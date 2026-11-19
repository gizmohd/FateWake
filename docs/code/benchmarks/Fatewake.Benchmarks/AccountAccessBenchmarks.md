# AccountAccessBenchmarks

Benchmarks the real PostgreSQL role and revocation lookup for active and disabled accounts. Set `FATEWAKE_AUTH_BENCHMARK_CONNECTION` to a disposable database, then run `dotnet run --project benchmarks\Fatewake.Benchmarks -c Release -- --filter '*AccountAccess*'`. Fixtures are removed on cleanup; migrations remain in that database.
