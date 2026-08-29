#!/usr/bin/env python3
"""Tải kho bằng chứng (trail) về thành một GÓI .zip để nạp vào công cụ kiểm chứng.

Vì sao cần script này: công cụ kiểm chứng là một trang tĩnh chạy trong trình duyệt, mà trình
duyệt chỉ đọc được kho khi CHÍNH KHO bật CORS — thứ nằm ngoài tầm tay người kiểm. Script chạy
ngoài trình duyệt nên không vướng CORS, và gói nó tạo ra dùng được cả trên bản offline.

Nguyên tắc, và cũng là lý do gói trông "thừa":
  · Gói chỉ chở BYTE THÔ. Script KHÔNG tính mã băm hộ từng lô — công cụ tự băm lại trên đúng
    byte trong gói. Script mà khai hộ mã băm thì chuỗi móc xích chỉ còn kiểm lời khai của script.
  · Gói chở NGUYÊN VĂN XML ListObjectsV2, không phải danh sách key đã bóc: danh sách và "đã đọc
    hết kho hay chưa" do công cụ tự suy ra bằng chính bộ bóc đang được test.
  · manifest.json chỉ là lời khai để hiển thị (địa chỉ kho, thời điểm tải), không đổi được kết
    luận nào.
  · Chỉ ký GET. Khoá có quyền ghi dán vào đây cũng không dựng nổi một request sửa kho.

Khoá KHÔNG nhận qua tham số dòng lệnh (dòng lệnh lộ ra ở `ps` và ở lịch sử shell): đặt biến môi
trường NOXH_KHO_MA_KHOA / NOXH_KHO_BI_MAT / NOXH_KHO_THE_PHIEN, hoặc chạy với --hoi-khoa để
script hỏi. Không có khoá thì tải ẩn danh (sau lễ, khi kho đã mở công khai).

Chỉ dùng thư viện chuẩn của Python 3 — máy tổ giám sát không phải cài gì thêm.

Ví dụ:
    python3 tai-goi-trail.py --diem-cuoi https://s3.vd-cloud.vn --bucket bang-chung \\
        --tien-to trail/ --vung us-east-1 --ra goi-trail.zip
"""

import argparse
import concurrent.futures
import datetime
import getpass
import hashlib
import hmac
import io
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

PHIEN_BAN_GOI = "NOXH-TRAIL-GOI-v1"
CONG_CU = "tai-goi-trail.py/1.0"

THU_MUC_TRANG = "listing/"
THU_MUC_NOI_DUNG = "objects/"

# SHA-256 của thân rỗng — mọi request ở đây đều là GET không thân.
BAM_THAN_RONG = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"

HAN_CHO_GIAY = 60
SO_LAN_THU = 3


# ── Ký SigV4 (chỉ GET) ──────────────────────────────────────────────────────────────────────


def ma_hoa_doan(doan):
    """Mã hoá một đoạn đường dẫn/tham số theo RFC 3986 — S3 KHÔNG mã hoá hai lần."""
    return urllib.parse.quote(doan, safe="-_.~")


def ma_hoa_duong_dan(duong_dan):
    return "/".join(ma_hoa_doan(d) for d in duong_dan.split("/"))


def chuoi_truy_van(tham_so):
    return "&".join(
        "%s=%s" % (ma_hoa_doan(t), ma_hoa_doan(g))
        for t, g in sorted(tham_so, key=lambda x: (ma_hoa_doan(x[0]), ma_hoa_doan(x[1])))
    )


def _hmac(khoa, chuoi):
    return hmac.new(khoa, chuoi.encode("utf-8"), hashlib.sha256).digest()


def ky_sigv4(duong_dan, tham_so, thoi_diem, host, vung, khoa):
    """Trả về các tiêu đề đã ký cho một GET. `khoa` là (ma_khoa, bi_mat, the_phien)."""
    ma_khoa, bi_mat, the_phien = khoa
    ngay_gio = thoi_diem.strftime("%Y%m%dT%H%M%SZ")
    ngay = thoi_diem.strftime("%Y%m%d")

    tieu_de = [
        ("host", host),
        ("x-amz-content-sha256", BAM_THAN_RONG),
        ("x-amz-date", ngay_gio),
    ]
    if the_phien:
        tieu_de.append(("x-amz-security-token", the_phien))
    tieu_de.sort()

    danh_sach_ten = ";".join(t for t, _ in tieu_de)
    canonical = "\n".join([
        "GET",
        ma_hoa_duong_dan(duong_dan),
        chuoi_truy_van(tham_so),
        "".join("%s:%s\n" % (t, g.strip()) for t, g in tieu_de),
        danh_sach_ten,
        BAM_THAN_RONG,
    ])

    pham_vi = "%s/%s/s3/aws4_request" % (ngay, vung)
    de_ky = "\n".join([
        "AWS4-HMAC-SHA256",
        ngay_gio,
        pham_vi,
        hashlib.sha256(canonical.encode("utf-8")).hexdigest(),
    ])

    k = _hmac(("AWS4" + bi_mat).encode("utf-8"), ngay)
    k = _hmac(k, vung)
    k = _hmac(k, "s3")
    k = _hmac(k, "aws4_request")
    chu_ky = hmac.new(k, de_ky.encode("utf-8"), hashlib.sha256).hexdigest()

    ra = {t: g for t, g in tieu_de if t != "host"}
    ra["Authorization"] = (
        "AWS4-HMAC-SHA256 Credential=%s/%s, SignedHeaders=%s, Signature=%s"
        % (ma_khoa, pham_vi, danh_sach_ten, chu_ky)
    )
    return ra


# ── Gọi kho ─────────────────────────────────────────────────────────────────────────────────


class LoiKho(Exception):
    pass


def goi(diem_cuoi, duong_dan, tham_so, vung, khoa):
    """Một GET tới kho, có thử lại. Trả về (byte thân, mã HTTP)."""
    tach = urllib.parse.urlsplit(diem_cuoi.rstrip("/"))
    truy_van = chuoi_truy_van(tham_so)
    url = "%s://%s%s%s" % (
        tach.scheme, tach.netloc, ma_hoa_duong_dan(duong_dan),
        ("?" + truy_van) if truy_van else "",
    )

    loi_cuoi = None
    for lan in range(SO_LAN_THU):
        tieu_de = {}
        if khoa:
            tieu_de = ky_sigv4(
                duong_dan, tham_so,
                datetime.datetime.now(datetime.timezone.utc),
                tach.netloc, vung, khoa,
            )
        try:
            yeu = urllib.request.Request(url, headers=tieu_de, method="GET")
            with urllib.request.urlopen(yeu, timeout=HAN_CHO_GIAY) as tra_loi:
                return tra_loi.read(), tra_loi.status
        except urllib.error.HTTPError as ex:
            than = ex.read()
            # Kho từ chối cũng trả XML có mã lỗi: giữ nguyên để gói còn nói được vì sao hỏng,
            # và đừng thử lại những lỗi mà thử lại cũng thế.
            if ex.code < 500:
                return than, ex.code
            loi_cuoi = "HTTP %d" % ex.code
        except (urllib.error.URLError, TimeoutError, OSError) as ex:
            loi_cuoi = str(ex)

        if lan < SO_LAN_THU - 1:
            time.sleep(2 ** lan)

    raise LoiKho(loi_cuoi or "không rõ lỗi")


def bo_ten_mien(the):
    return the.tag.split("}")[-1]


def boc_trang(xml_byte):
    """Trả về (danh sách key, dấu tiếp tục, mã lỗi kho nếu có)."""
    goc = ET.fromstring(xml_byte)

    if bo_ten_mien(goc) == "Error":
        ma = "".join(c.text or "" for c in goc if bo_ten_mien(c) == "Code")
        return [], None, ma or "không rõ mã lỗi"

    key, con_nua, dau = [], False, None
    for con in goc:
        ten = bo_ten_mien(con)
        if ten == "Contents":
            for chau in con:
                if bo_ten_mien(chau) == "Key" and chau.text:
                    key.append(chau.text)
        elif ten == "IsTruncated":
            con_nua = (con.text or "").strip().lower() == "true"
        elif ten == "NextContinuationToken":
            dau = con.text

    return key, (dau if con_nua else None), None


# ── Dựng gói ────────────────────────────────────────────────────────────────────────────────


def tai_kho(tuy_chon, khoa):
    """Trả về (các trang XML nguyên văn, danh sách key, nội dung từng key, các key hỏng)."""
    trang, key = [], []
    dau_tiep_tuc = None

    while True:
        tham_so = [("list-type", "2")]
        if tuy_chon.tien_to:
            tham_so.append(("prefix", tuy_chon.tien_to))
        if dau_tiep_tuc:
            tham_so.append(("continuation-token", dau_tiep_tuc))

        than, ma_http = goi(
            tuy_chon.diem_cuoi, "/" + tuy_chon.bucket, tham_so, tuy_chon.vung, khoa)
        trang.append(than)

        key_trang, dau_moi, loi_kho = boc_trang(than)
        if loi_kho:
            raise LoiKho("kho từ chối liệt kê: %s (HTTP %d)" % (loi_kho, ma_http))
        if ma_http >= 300:
            raise LoiKho("kho trả HTTP %d khi liệt kê" % ma_http)

        key.extend(key_trang)
        print("  … trang %d: %d object (tổng %d)" % (len(trang), len(key_trang), len(key)))

        # Kho trả cùng một dấu tiếp tục thì vòng lặp này quay mãi — dừng và khai là chưa hết.
        if dau_moi is None or dau_moi == dau_tiep_tuc:
            if dau_moi is not None:
                print("  ! kho lặp lại dấu tiếp tục — dừng liệt kê, gói sẽ KHÔNG đủ", file=sys.stderr)
            break
        dau_tiep_tuc = dau_moi

    noi_dung, hong = {}, []

    def tai_mot(mot_key):
        than, ma_http = goi(
            tuy_chon.diem_cuoi, "/%s/%s" % (tuy_chon.bucket, mot_key),
            [], tuy_chon.vung, khoa)
        if ma_http >= 300:
            raise LoiKho("HTTP %d" % ma_http)
        return than

    with concurrent.futures.ThreadPoolExecutor(max_workers=tuy_chon.song_song) as bo:
        viec = {bo.submit(tai_mot, k): k for k in key}
        xong = 0
        for tuong_lai in concurrent.futures.as_completed(viec):
            mot_key = viec[tuong_lai]
            try:
                noi_dung[mot_key] = tuong_lai.result()
            except Exception as ex:  # noqa: BLE001 — hỏng một lô không được làm hỏng cả lần tải
                hong.append((mot_key, str(ex)))
            xong += 1
            if xong % 100 == 0 or xong == len(key):
                print("  … tải %d/%d lô" % (xong, len(key)))

    return trang, key, noi_dung, hong


def viet_goi(duong_dan_ra, tuy_chon, che_do, trang, key, noi_dung):
    """Ghi gói .zip. Mốc thời gian cố định + thứ tự cố định: cùng nội dung ⇒ cùng SHA-256, nên
    hai người tải độc lập đối chiếu được mã băm gói của nhau."""
    moc = (1980, 1, 1, 0, 0, 0)
    khai_bao = {
        "phienBan": PHIEN_BAN_GOI,
        "congCu": CONG_CU,
        "diemCuoi": tuy_chon.diem_cuoi.rstrip("/"),
        "bucket": tuy_chon.bucket,
        "tienTo": tuy_chon.tien_to,
        "vung": tuy_chon.vung,
        "cheDoDoc": che_do,
        "taoLuc": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "soTrangDanhSach": len(trang),
        "soObjectTrongDanhSach": len(key),
        "soObjectDaTai": len(noi_dung),
    }
    # Tự viết JSON để khỏi kéo thêm gì: manifest chỉ là lời khai, khuôn phẳng, không cần escape lạ.
    dong = ",\n".join(
        '  "%s": %s' % (t, ('"%s"' % str(g).replace('"', '\\"')) if isinstance(g, str) else g)
        for t, g in khai_bao.items()
    )
    manifest = ("{\n%s\n}\n" % dong).encode("utf-8")

    bo_nho = io.BytesIO()
    with zipfile.ZipFile(bo_nho, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as goi_zip:
        def them(ten, du_lieu):
            thong_tin = zipfile.ZipInfo(ten, date_time=moc)
            thong_tin.compress_type = zipfile.ZIP_DEFLATED
            thong_tin.external_attr = 0o644 << 16
            goi_zip.writestr(thong_tin, du_lieu)

        them("manifest.json", manifest)
        for i, than in enumerate(trang):
            them("%s%03d.xml" % (THU_MUC_TRANG, i), than)
        for mot_key in sorted(noi_dung):
            them(THU_MUC_NOI_DUNG + mot_key, noi_dung[mot_key])

    byte_goi = bo_nho.getvalue()
    with open(duong_dan_ra, "wb") as ra:
        ra.write(byte_goi)

    return hashlib.sha256(byte_goi).hexdigest(), len(byte_goi)


# ── Vào ─────────────────────────────────────────────────────────────────────────────────────


def doc_khoa(hoi):
    ma_khoa = os.environ.get("NOXH_KHO_MA_KHOA", "")
    bi_mat = os.environ.get("NOXH_KHO_BI_MAT", "")
    the_phien = os.environ.get("NOXH_KHO_THE_PHIEN", "") or None

    if hoi:
        ma_khoa = input("Mã khoá truy cập: ").strip()
        bi_mat = getpass.getpass("Khoá bí mật: ").strip()
        the_phien = getpass.getpass("Thẻ phiên (Enter nếu không có): ").strip() or None

    if ma_khoa and bi_mat:
        return (ma_khoa, bi_mat, the_phien)
    return None


def tu_kiem():
    """Tự kiểm phần ký, offline. Chạy trước lễ: ký sai một byte thì kho từ chối, mà lúc đó đã muộn.

    Số đem so là số AWS công bố (tài liệu "Signature Calculation Examples"), không phải số do chính
    script này sinh ra — ghim bằng kết quả của chính mình thì ghim cái gì cũng "đúng"."""
    luc = datetime.datetime(2013, 5, 24, tzinfo=datetime.timezone.utc)
    ra = ky_sigv4(
        "/", [("lifecycle", "")], luc, "examplebucket.s3.amazonaws.com", "us-east-1",
        ("AKIAIOSFODNN7EXAMPLE", "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", None))
    mong = (
        "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request, "
        "SignedHeaders=host;x-amz-content-sha256;x-amz-date, "
        "Signature=fea454ca298b7da1c68078a5d1bdbfbbe0d65c699e0f91ac7a200a0136783543")

    hong = []
    if ra["Authorization"] != mong:
        hong.append("chữ ký SigV4 lệch vector chuẩn AWS:\n  ra:   %s\n  mong: %s"
                    % (ra["Authorization"], mong))
    if hashlib.sha256(b"").hexdigest() != BAM_THAN_RONG:
        hong.append("hằng số mã băm thân rỗng sai")
    if ma_hoa_duong_dan("/bang-chung/trail/2026/08/15/a b+c.jsonl") \
            != "/bang-chung/trail/2026/08/15/a%20b%2Bc.jsonl":
        hong.append("mã hoá đường dẫn sai (ký tự lạ trong key sẽ làm kho từ chối)")

    for loi in hong:
        print("TỰ KIỂM HỎNG: %s" % loi, file=sys.stderr)

    if hong:
        return 3

    print("Tự kiểm ĐẠT: chữ ký khớp vector chuẩn AWS công bố.")
    return 0


def main(doi_so):
    bo_doc = argparse.ArgumentParser(
        description="Tải kho bằng chứng về thành gói .zip cho công cụ kiểm chứng NƠXH.")
    bo_doc.add_argument("--tu-kiem", action="store_true",
                        help="tự kiểm phần ký theo vector chuẩn AWS rồi thoát (không gọi mạng)")
    bo_doc.add_argument("--diem-cuoi", help="ví dụ https://s3.vd-cloud.vn")
    bo_doc.add_argument("--bucket")
    bo_doc.add_argument("--tien-to", default="trail/")
    bo_doc.add_argument("--vung", default="us-east-1")
    bo_doc.add_argument("--ra", default=None, help="đường dẫn file gói .zip sẽ ghi ra")
    bo_doc.add_argument("--song-song", type=int, default=8)
    bo_doc.add_argument("--hoi-khoa", action="store_true",
                        help="hỏi khoá chỉ-đọc ngay tại đây thay vì đọc biến môi trường")
    tuy_chon = bo_doc.parse_args(doi_so)

    if tuy_chon.tu_kiem:
        return tu_kiem()

    if not tuy_chon.diem_cuoi or not tuy_chon.bucket:
        bo_doc.error("thiếu --diem-cuoi hoặc --bucket")

    if tuy_chon.ra is None:
        tuy_chon.ra = "goi-trail-%s.zip" % datetime.datetime.now(
            datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")

    khoa = doc_khoa(tuy_chon.hoi_khoa)
    che_do = "khoa-chi-doc" if khoa else "an-danh"
    print("Kho: %s/%s/%s — chế độ %s" % (
        tuy_chon.diem_cuoi.rstrip("/"), tuy_chon.bucket, tuy_chon.tien_to, che_do))

    try:
        trang, key, noi_dung, hong = tai_kho(tuy_chon, khoa)
    except LoiKho as ex:
        print("Tải kho không được: %s" % ex, file=sys.stderr)
        return 2

    ma_bam, kich_thuoc = viet_goi(tuy_chon.ra, tuy_chon, che_do, trang, key, noi_dung)

    print("")
    print("Gói: %s (%.1f MB)" % (tuy_chon.ra, kich_thuoc / 1024 / 1024))
    print("SHA-256 gói: %s" % ma_bam)
    print("Trang danh sách: %d · object trong danh sách: %d · đã tải: %d"
          % (len(trang), len(key), len(noi_dung)))

    if hong:
        # Lô tải hỏng KHÔNG bị giấu: gói thiếu nó, công cụ sẽ báo lô đó chưa kiểm được.
        print("", file=sys.stderr)
        print("%d lô tải không được — gói THIẾU các lô này:" % len(hong), file=sys.stderr)
        for mot_key, loi in hong[:20]:
            print("  · %s (%s)" % (mot_key, loi), file=sys.stderr)
        if len(hong) > 20:
            print("  · … và %d lô nữa" % (len(hong) - 20), file=sys.stderr)
        print("Hãy chạy lại để tải đủ trước khi đem gói đi kiểm.", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
