using System;
using FluentAssertions.Equivalency.Tracing;
using FluentAssertions.Execution;

namespace FluentAssertions.Equivalency;

/// <summary>
/// Is responsible for validating the equivalency of a subject with another object.
/// </summary>
public class EquivalencyValidator : IEquivalencyValidator
{
    private const int MaxDepth = 10;

    public void AssertEquality(Comparands comparands, EquivalencyValidationContext context)
    {
        using var scope = new AssertionScope();

        scope.AssumeSingleCaller();
        scope.AddReportable("configuration", () => context.Options.ToString());
        scope.BecauseOf(context.Reason);

        RecursivelyAssertEquality(comparands, context);

        if (context.TraceWriter is not null)
        {
            scope.AppendTracing(context.TraceWriter.ToString());
        }
    }

    public void RecursivelyAssertEquality(Comparands comparands, IEquivalencyValidationContext context)
    {
        var scope = AssertionScope.Current;

        if (ShouldContinueThisDeep(context.CurrentNode, context.Options, scope))
        {
            TrackWhatIsNeededToProvideContextToFailures(scope, comparands, context.CurrentNode);

            if (!context.IsCyclicReference(comparands.Expectation))
            {
                TryToProveNodesAreEquivalent(comparands, context);
            }
        }
    }

    private static bool ShouldContinueThisDeep(INode currentNode, IEquivalencyOptions options,
        AssertionScope assertionScope)
    {
        bool shouldRecurse = options.AllowInfiniteRecursion || currentNode.Depth <= MaxDepth;
        if (!shouldRecurse)
        {
            // This will throw, unless we're inside an AssertionScope
            assertionScope.FailWith($"The maximum recursion depth of {MaxDepth} was reached.  ");
        }

        return shouldRecurse;
    }

    private static void TrackWhatIsNeededToProvideContextToFailures(AssertionScope scope, Comparands comparands, INode currentNode)
    {
        scope.Context = new Lazy<string>(() => currentNode.Description);

        scope.TrackComparands(comparands.Subject, comparands.Expectation);
    }

    private void TryToProveNodesAreEquivalent(Comparands comparands, IEquivalencyValidationContext context)
    {
        using var _ = context.Tracer.WriteBlock(node => node.Description);

        Func<IEquivalencyStep, GetTraceMessage> getMessage = step => _ => $"Equivalency was proven by {step.GetType().Name}";

        foreach (IEquivalencyStep step in AssertionOptions.EquivalencyPlan)
        {
            var result = step.Handle(comparands, context, this);
            if (result == EquivalencyResult.AssertionCompleted)
            {
                context.Tracer.WriteLine(getMessage(step));
                return;
            }
        }

        throw new NotSupportedException(
            $"Do not know how to compare {comparands.Subject} and {comparands.Expectation}. Please report an issue through https://www.fluentassertions.com.");
    }
}
