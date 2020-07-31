#!/bin/bash

grep=""
NoReport=0

while [[ "$#" -gt 0 ]]; do
    case $1 in
        -g|--grep) grep="$2"; shift ;;
        --no-report) NoReport=1 ;;
        -s) Suffix="$2"; shift ;;
        *) echo "Unknown parameter passed: $1"; exit 1 ;;
    esac
    shift
done

CASES="./test-cases/$grep*"
PASSWORD="Passw0rd1"
ConnectionString="Server=tcp:localhost,1433;User ID=sa;Password=$PASSWORD"

trap 'exit 1' int

for caseDir in $CASES
do
  if [ ! -d "$caseDir" ]
  then
    continue
  fi

  CASE=$(basename "$caseDir")
  CASE_ConnectionString="$ConnectionString;Initial Catalog=$CASE"

  echo "Initialize database for test case $CASE"
  docker-compose exec -T mssql /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P $PASSWORD -Q "drop database IF EXISTS [$CASE]"
  docker-compose exec -T mssql /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P $PASSWORD -Q "create database [$CASE]"
  docker-compose exec -T mssql /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P $PASSWORD -d "$CASE" -i "$caseDir/create.sql" -I

  echo "Running direct mapping"
  r2rml4net direct \
    -c "$CASE_ConnectionString" \
    -o "$caseDir/directGraph-$Suffix.ttl" \
    -b http://example.com/base/ \
    --preserve-duplicate-rows

  echo "Running R2RML test cases"

  RML_CASES="$caseDir/r2rml*.ttl"

  shopt -s nullglob
  for rml in $RML_CASES
  do
    RML_OUT=$(echo "$rml" | sed -e 's/r2rml/mapped/g' -e "s/\.ttl$/-$Suffix.nq/g")
    r2rml4net rml \
      -c "$CASE_ConnectionString" \
      -m "$rml" \
      -o "$RML_OUT" \
      -b http://example.com/base/
  done
done

if [ $NoReport -eq 0 ]
then
  echo "Generating EARL reports"
  (
    cd "test harness"
    ./rdb2rdf-th ts.ttl
    mkdir -p ../reports
    mv $(echo earl*.ttl) ../reports
  )
fi
