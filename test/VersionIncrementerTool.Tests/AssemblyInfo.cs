// Application reads Environment.CurrentDirectory and writes to Console, so tests must not run in parallel
[assembly: CollectionBehavior(DisableTestParallelization = true)]
