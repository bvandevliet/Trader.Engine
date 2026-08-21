using System.Runtime.CompilerServices;

// BudgetLedger (and any other internal type worth testing in isolation, without making it public
// API) needs direct test access for concurrency stress testing.
[assembly: InternalsVisibleTo("TraderEngine.Common.Tests")]
