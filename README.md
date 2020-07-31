# R2RML test runner

Runs W3C triple mapping test cases against R2RML and Direct Mapping implementations

This is a template which can be used to set up tests for your implementation of R2RML/DM

## What it does

1. Runs R2RML and/or Direct Mapping against databases running in containers
2. Generates EARL reports using the official R2RML test harness
2. Publishes said reports to GitHub Pages

## Databases

- ✅ MS SQL Server
- 🚧 MySQL
- 🚧 Postgres
- 🚧 Oracle
- ❣️ Create issue for others

## Steps to customize

1. Clone this repo or use as template
2. Add necessary init steps to (Implementation report.yml)[r2rml4net-implementation-report/.github/workflows/Implementation report.yml] GitHub Action
2. Fill in your tool's manifest in [./test harness/ts.ttl](./test%20harness/ts.ttl)
2. If supporting R2RML, add command to run your tool in [./scripts/r2rml.sh](./scripts/r2rml.sh)
3. If supporting DM, add command to run your tool in [./scripts/direct.sh](./scripts/direct.sh)
4. Submit a PR to [r2rml4net/reports](https://github.com/r2rml4net/reports) have test results published online

## Steps to run (what the CI does)

```
docker-compose up -d
git clone https://github.com/r2rml4net/test-cases.git --depth 1
./index.js
```
