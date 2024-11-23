var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
    .WithRedisCommander()
    .WithRedisInsight()
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.HybridCaching_Tests>("hybrid-caching-api")
    .WithExternalHttpEndpoints()
    .WithReference(redis)
    .WaitFor(redis);


builder.AddProject<Projects.HybridCaching_Tests>("hybrid-caching-api-2")
    .WithExternalHttpEndpoints()
    .WithReference(redis)
    .WaitFor(redis);

builder.Build().Run();