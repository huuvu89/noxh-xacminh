#!/usr/bin/env bash
# Sinh lại fixture chuẩn vàng từ backend thật. Xem fixtures/README.md.
#
#   bash fixtures/generator/sinh-lai-fixture.sh [đường-dẫn-repo-backend]
#
# Yêu cầu: .NET 8 SDK + Docker đang chạy (integration test của backend dựng Postgres/Redis thật
# bằng Testcontainers).
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
backend="${1:-$(cd "$repo/../backend-noxh-lottery" && pwd)}"

if [ ! -d "$backend/tests/Lottery.IntegrationTests" ]; then
  echo "Không thấy bộ integration test của backend ở: $backend" >&2
  exit 1
fi

dirty="$(git -C "$backend" status --porcelain | wc -l)"
if [ "$dirty" != "0" ]; then
  echo "CẢNH BÁO: cây làm việc của backend đang bẩn — fixture sẽ không truy được về đúng commit:" >&2
  git -C "$backend" status --porcelain >&2
fi

export NOXH_FIXTURE_OUT="$repo/fixtures/transparency-golden.json"
export NOXH_BACKEND_COMMIT="$(git -C "$backend" log -1 --format=%H)"
export NOXH_BACKEND_COMMITTED_AT="$(git -C "$backend" log -1 --format=%cI)"

echo "Backend: $backend @ $NOXH_BACKEND_COMMIT"
echo "Fixture: $NOXH_FIXTURE_OUT"

dotnet test "$here/Noxh.XacMinh.FixtureGen.csproj" -p:BackendRepo="$backend" --nologo "${@:2}"
