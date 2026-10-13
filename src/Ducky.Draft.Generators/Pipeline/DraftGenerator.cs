using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ducky.Shared.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ducky.Draft.Generators;

/// <summary>Emits <c>Produce</c>, <c>CreateDraft</c> and the nested <c>Draft</c> type of <c>[Draftable]</c> records (SPEC §13.3).</summary>
[Generator(LanguageNames.CSharp)]
public sealed class DraftGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var records = context.SyntaxProvider
            .ForAttributeWithMetadataName("Ducky.Draft.DraftableAttribute", static (n, _) => n is TypeDeclarationSyntax, static (c, _) => DraftModel.Create(c))
            .WithTrackingName("DraftRecords");
        var output = records.Collect().WithTrackingName("DraftOutput");
        context.RegisterSourceOutput(output, static (spc, models) => Emit(spc, models));
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<DraftModel> models)
    {
        var hintNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase); // AddSource ignores case too
        foreach (var model in models)
        {
            if (model.Error is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(model.Error, model.Location, model.DisplayName, model.ErrorArgument));
                continue;
            }

            var baseName = IdentifierSanitizer.HintName(model.DisplayName);
            var hintName = baseName;
            for (var i = 2; !hintNames.Add(hintName); i++)
            {
                hintName = $"{baseName}_{i}";
            }

            context.AddSource(hintName + ".Draft.g.cs", Source(model));
        }
    }

    private static string Source(DraftModel model)
    {
        var ids = string.Join(", ", model.DiagnosticIds.Items);
        var hasIds = model.DiagnosticIds.Items.Length > 0;
        var writer = new CodeWriter()
            .Line("#pragma warning disable CS0612, CS0618 // justification: generated code references user [Obsolete] members (GEN-06)"
                + (hasIds ? $"\n#pragma warning disable {ids} // justification: generated code references user [Obsolete] or [Experimental] members with their own diagnostic id (GEN-06)" : ""))
            .Line(model.Namespace is null ? "" : $"namespace {model.Namespace};\n");
        foreach (var container in model.Containers.Items)
        {
            writer.Open(container);
        }

        writer.Line(Body(model));
        foreach (var _ in model.Containers.Items)
        {
            writer.Close();
        }

        return writer
            .Line("#pragma warning restore CS0612, CS0618" + (hasIds ? $"\n#pragma warning restore {ids}" : ""))
            .ToString();
    }

    private static string Body(DraftModel model)
    {
        var type = model.TypeName;
        var draft = $"global::Ducky.Draft.IDraft<{type}>";
        var newDraft = model.NewDraft ? "new " : "";
        var newCreateDraft = model.NewCreateDraft ? "new " : "";
        var writable = model.Members.Items.Where(m => m.Setter is not null).ToList();
        var fields = writable.Select(m => $"\n    private {m.Type} __m_{m.Name};");
        var copies = writable.Select(m => $"\n        __m_{m.Name} = original.{Escape(m)};");
        var changes = writable.Select(m => $"!global::System.Collections.Generic.EqualityComparer<{m.Type}>.Default.Equals(__m_{m.Name}, __x_original.{Escape(m)})");
        var assignments = writable.Select(m => $"{Escape(m)} = __m_{m.Name}{(m.NullForgiving ? "!" : "")}");
        var build = writable.Count == 0
            ? "return __x_original;"
            : $"return (({draft})this).IsDirty ? __x_original with {{ {string.Join(", ", assignments)} }} : __x_original;";
        return $$"""
            /// <summary>Applies <paramref name="recipe"/> to a draft of this instance and returns the result (this instance when nothing changed).</summary>
            public {{type}} Produce(global::System.Action<Draft> recipe)
            {
                var draft = new Draft(this);
                try
                {
                    recipe(draft);
                    return (({{draft}})draft).Build();
                }
                finally
                {
                    ((global::Ducky.Draft.IRevocable)draft).Revoke();
                }
            }

            /// <summary>Creates a draft of this instance for multi-step editing; build it to get the result.</summary>
            public {{newCreateDraft}}Draft CreateDraft() => new(this);

            /// <summary>A copy-on-write draft of <see cref="{{type}}"/>: building it returns the original instance when every member still equals the original's.</summary>
            public {{newDraft}}sealed class Draft : {{draft}}
            {
                private readonly {{type}} __x_original;
                private bool __x_revoked;{{string.Concat(fields)}}

                /// <summary>Creates a draft of <paramref name="original"/>.</summary>
                public Draft({{type}} original)
                {
                    __x_original = original;{{string.Concat(copies)}}
                }

                /// <summary>Creates a draft of <paramref name="value"/>, or null for null.</summary>
                [return: global::System.Diagnostics.CodeAnalysis.NotNullIfNotNull("value")]
                public static implicit operator Draft?({{type}}? value) => value is null ? null : new(value);{{string.Concat(model.Members.Items.Select(Property))}}

                bool {{draft}}.IsDirty => {{(writable.Count == 0 ? "false" : string.Join("\n        || ", changes))}};

                {{type}} {{draft}}.Build()
                {
                    __x_Guard();
                    {{build}}
                }{{(model.HasBuild ? "" : $"""


                /// <summary>Builds the result: the original instance when nothing changed, else a new instance.</summary>
                public {type} Build() => (({draft})this).Build();
            """)}}

                void global::Ducky.Draft.IRevocable.Revoke() => __x_revoked = true;

                private void __x_Guard()
                {
                    if (__x_revoked)
                    {
                        throw new global::System.ObjectDisposedException("Draft", "draft revoked: Produce returned, or an assignment or structural operation replaced it");
                    }
                }
            }
            """;
    }

    private static string Property(MemberModel member)
    {
        var header = $"{(member.DisallowNull ? "[global::System.Diagnostics.CodeAnalysis.DisallowNull] " : "")}{(member.IsPublic ? "public" : "internal")}{(member.IsNew ? " new" : "")} {member.Type} {Escape(member)}";
        return member.Setter is null
            ? $$"""


                    /// <summary>Gets <c>{{member.Name}}</c> of the original (the draft can't set it).</summary>
                    {{header}}
                    {
                        get { __x_Guard(); return __x_original.{{Escape(member)}}; }
                    }
                """
            : $$"""


                    /// <summary>Gets or sets <c>{{member.Name}}</c>.</summary>
                    {{header}}
                    {
                        get { __x_Guard(); return __m_{{member.Name}}; }
                        {{member.Setter}}set { __x_Guard(); __m_{{member.Name}} = value; }
                    }
                """;
    }

    private static string Escape(MemberModel member) => IdentifierSanitizer.EscapeKeyword(member.Name);
}
