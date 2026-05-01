# Local Setup

## Day 1 PM Backend Bootstrap

Run the full Day 1 PM backend setup (infra + db role fix + migrations + seed):

```bash
./scripts/bootstrap-day1pm.sh
```

This script specifically fixes the migration error:

`role "escaperoom" does not exist`

It appears when an older persisted Postgres volume exists without the `escaperoom` role/user. The script idempotently creates the role and database before running EF migrations.

## Manual Recovery (if needed)

If your Docker state is very stale and you want a clean reset:

```bash
docker compose -f infra/docker-compose.yml down -v
docker compose -f infra/docker-compose.yml up -d
./scripts/bootstrap-day1pm.sh
```
