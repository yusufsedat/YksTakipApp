#!/bin/sh
# Railway Pre-Deploy: ADO.NET connection string ile efbundle çalıştırır.
# mysql:// URL veya boş değişken ile anlamlı hata verir.
set -eu
cd /app

conn="${ConnectionStrings__DefaultConnection:-}"
if [ -z "$conn" ]; then
  echo "railway-migrate: ConnectionStrings__DefaultConnection bos veya tanimli degil." >&2
  exit 1
fi

case "$conn" in
  mysql://*|MYSQL://*)
    echo "railway-migrate: mysql:// URL kullanma. Server=host;Port=3306;Database=...;User=...;Password=...; biciminde Pomelo connection string kullan." >&2
    exit 1
    ;;
esac

if [ ! -x ./efbundle ]; then
  echo "railway-migrate: /app/efbundle bulunamadi veya calistirilamiyor." >&2
  exit 1
fi

echo "railway-migrate: efbundle calisiyor..."
exec ./efbundle --connection "$conn"
