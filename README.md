# Datasync conflict repro: stale UI save after sync

Saving a tracked entity after another context’s push/pull queues the **pre-sync `Version`**, so the next push **409**s against the client’s own earlier write.

```
UI loads @ V1 → edit #1 → Push/Pull (→ V2)
UI still tracks V1 → edit #2 SaveChanges → Push #2 → 409
```

**Fix:** detach, reload from SQLite, apply changes, then save (`Client/RebaseTodoSave.cs`).

```bash
dotnet test
```
