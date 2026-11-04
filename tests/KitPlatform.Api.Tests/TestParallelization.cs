using Xunit;

// Every class here writes to the same DEMO_PHARMACY database (orders, lot quantities, tenant batch mode),
// and some assert exact counts, so classes must not run at the same time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
