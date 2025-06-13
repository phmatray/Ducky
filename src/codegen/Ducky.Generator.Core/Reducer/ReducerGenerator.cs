namespace Ducky.Generator.Core;

public class ReducerGenerator : SourceGeneratorBase<ReducerGeneratorOptions>
{
    protected override CompilationUnitElement BuildModel(ReducerGeneratorOptions opts)
    {
        return new CompilationUnitElement()
        {
            Usings = [
                "System",
                opts.Namespace // for your domain types
            ],
            Namespaces = [
                new NamespaceElement()
                {
                    Name = opts.Namespace,
                    Classes = opts.Reducers.Select(r =>
                        new ClassElement()
                        {
                            Name = r.ReducerClassName,
                            IsStatic = true,
                            Methods = BuildReducerMethods(r)
                        })
                }
            ]
        };
    }

    private static IEnumerable<MethodElement> BuildReducerMethods(ReducerDescriptor desc)
    {
        // 1) Emit the partial-­method signatures
        foreach (string actionName in desc.Actions)
        {
            // derive “OnAdd” from “AddTodoAction”, etc.
            string trimmed = actionName.EndsWith("Action")
                ? actionName[..^"Action".Length]
                : actionName;

            // take the initial verb chunk ("Add" from "AddTodo")
            int split = 1;
            while (split < trimmed.Length && !char.IsUpper(trimmed[split]))
            {
                split++;
            }

            string verb = trimmed[..split];

            yield return new MethodElement()
            {
                Name = "On" + verb,
                ReturnType = desc.StateType,
                Parameters = [
                    new()
                    {
                        ParamName = "state",
                        ParamType = desc.StateType
                    },
                    new()
                    {
                        ParamName = char.ToLowerInvariant(trimmed[0]) + trimmed[1..],
                        ParamType = actionName
                    }
                ],
                IsPartialDeclaration = true
            };
        }

        // 2) Emit the single Reduce(...) expression-bodied method
        IEnumerable<string> arms = desc.Actions
            .Select(actionName =>
            {
                string trimmed = actionName.EndsWith("Action")
                    ? actionName[..^"Action".Length]
                    : actionName;
                // single-letter var: 'a', 't', 'r', etc.
                string varName = char.ToLowerInvariant(trimmed[0]).ToString();
                // same verb logic as above
                int split = 1;
                while (split < trimmed.Length && !char.IsUpper(trimmed[split]))
                {
                    split++;
                }

                string verb = trimmed[..split];

                return $"{actionName} {varName} => On{verb}(state, {varName})";
            })
            .Append("_ => state");

        string switchExpr =
            "action switch\n"
                + "{\n    "
                + string.Join(",\n    ", arms)
                + "\n}";

        yield return new MethodElement()
        {
            Name = "Reduce",
            ReturnType = desc.StateType,
            Parameters = [
                new()
                {
                    ParamName = "state",
                    ParamType = desc.StateType
                },
                new()
                {
                    ParamName = "action",
                    ParamType = "object"
                }
            ],
            ExpressionBody = new ExpressionElement() { Code = switchExpr }
        };
    }
}
