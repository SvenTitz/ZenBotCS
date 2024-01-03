dotnet ef migrations add YourMigrationName `
    --project $PSScriptRoot/.. `
    --context CocApi.Cache.CacheDbContext `
    -o ./OutputFolder