#!/bin/bash
set -u

grep=""
NoReport=0

while [[ "$#" -gt 0 ]]; do
    case $1 in
        -g|--grep) grep="$2"; shift ;;
        --no-report) NoReport=1 ;;
        -s) Suffix="$2"; shift ;;
        --engine) engine="$2"; shift ;;
        *) echo "Unknown parameter passed: $1"; exit 1 ;;
    esac
    shift
done

CASES="./test-cases/$grep*"

trap 'exit 1' int

for caseDir in $CASES
do
  if [ ! -d "$caseDir" ]
  then
    continue
  fi

  CASE=$(basename "$caseDir")

  echo "Initialize database for test case $CASE"
  ./scripts/init-db.sh \
    --engine "$engine" \
    --database "$CASE" \
    --script "$caseDir/create.sql"

  echo "Running direct mapping"
  ./scripts/direct.sh \
    --database "$CASE" \
    --output "$caseDir/directGraph-$Suffix.ttl" \
    --base http://example.com/base/ \
    --engine "$engine"

  echo "Running R2RML test cases"

  RML_CASES="$caseDir/r2rml*.ttl"

  shopt -s nullglob
  for rml in $RML_CASES
  do
    RML_OUT=$(echo "$rml" | sed -e 's/r2rml/mapped/g' -e "s/\.ttl$/-$Suffix.nq/g")
    ./scripts/r2rml.sh \
      --database "$CASE" \
      --mapping "$rml" \
      --output "$RML_OUT" \
      --base http://example.com/base/ \
      --engine "$engine"
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
