using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Ducky.CodeGen.Core;

// === Model Interfaces ===
public interface ICodeElement
{
    T Accept<T>(ISyntaxVisitor<T> visitor);
}

public interface ISyntaxVisitor<T>
{
    T Visit(CompilationUnitElement unit);
    T Visit(NamespaceElement ns);
    T Visit(ClassElement cls);
    T Visit(PropertyElement property);
    T Visit(MethodElement method);
    T Visit(ExpressionElement expr);
}

// === Model Classes ===
public class CompilationUnitElement : ICodeElement
{
    public IEnumerable<string> Usings { get; set; } = Array.Empty<string>();
    public IEnumerable<NamespaceElement> Namespaces { get; set; } = Array.Empty<NamespaceElement>();

    public T Accept<T>(ISyntaxVisitor<T> visitor)
        => visitor.Visit(this);
}

public class NamespaceElement : ICodeElement
{
    public string Name { get; set; } = string.Empty;
    public IEnumerable<ClassElement> Classes { get; set; } = Array.Empty<ClassElement>();

    public T Accept<T>(ISyntaxVisitor<T> visitor)
        => visitor.Visit(this);
}

public class ClassElement : ICodeElement
{
    public string Name { get; set; } = string.Empty;
    public bool IsStatic { get; set; } = true;
    public bool IsPartial { get; set; } = false;
    public bool IsAbstract { get; set; } = false;
    public string BaseClass { get; set; } = string.Empty;
    public IEnumerable<string> Interfaces { get; set; } = Array.Empty<string>();
    public IEnumerable<PropertyElement> Properties { get; set; } = Array.Empty<PropertyElement>();
    public IEnumerable<MethodElement> Methods { get; set; } = Array.Empty<MethodElement>();

    public T Accept<T>(ISyntaxVisitor<T> visitor)
        => visitor.Visit(this);
}

public class PropertyElement : ICodeElement
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Accessibility { get; set; } = "public";
    public bool HasGetter { get; set; } = true;
    public bool HasSetter { get; set; } = true;
    public bool IsStatic { get; set; } = false;
    public ExpressionElement? GetterBody { get; set; }
    public ExpressionElement? DefaultValue { get; set; }
    public IEnumerable<string> Attributes { get; set; } = Array.Empty<string>();
    public string XmlDocumentation { get; set; } = string.Empty;

    public T Accept<T>(ISyntaxVisitor<T> visitor) => visitor.Visit(this);
}

public class MethodElement : ICodeElement
{
    public string Name { get; set; } = string.Empty;

    public string ReturnType { get; set; } = "void";

    /// <summary>
    /// If true, the first parameter will get a `this` modifier.
    /// </summary>
    public bool IsExtensionMethod { get; set; }

    /// <summary>
    /// If true and no body is supplied, we'll emit a `private static partial …;` declaration.
    /// </summary>
    public bool IsPartialDeclaration { get; set; }

    // now an ordered list of parameters
    public IEnumerable<ParameterDescriptor> Parameters { get; set; } = Array.Empty<ParameterDescriptor>();

    // if set, we emit => body
    public ExpressionElement? ExpressionBody { get; set; }

    // XML documentation comments
    public string XmlDocumentation { get; set; } = string.Empty;

    // Method body for non-expression-bodied methods
    public ExpressionElement? MethodBody { get; set; }

    public T Accept<T>(ISyntaxVisitor<T> visitor) => visitor.Visit(this);
}

public class ExpressionElement : ICodeElement
{
    public string Code { get; set; } = string.Empty; // raw or could be parsed more strongly

    public T Accept<T>(ISyntaxVisitor<T> visitor)
        => visitor.Visit(this);
}

// === Concrete Visitor Implementation ===
public class SyntaxFactoryVisitor : ISyntaxVisitor<SyntaxNode>
{
    public SyntaxNode Visit(CompilationUnitElement unit)
    {
        UsingDirectiveSyntax[] usingDirectives = unit.Usings
            .Select(u => UsingDirective(ParseName(u)))
            .ToArray();
        MemberDeclarationSyntax[] namespaceNodes = unit.Namespaces
            .Select(ns => (NamespaceDeclarationSyntax)ns.Accept(this))
            .ToArray<MemberDeclarationSyntax>();
        return CompilationUnit()
            .AddUsings(usingDirectives)
            .AddMembers(namespaceNodes)
            .NormalizeWhitespace();
    }

    public SyntaxNode Visit(NamespaceElement ns)
    {
        MemberDeclarationSyntax[] classes = ns.Classes
            .Select(c => (ClassDeclarationSyntax)c.Accept(this))
            .ToArray<MemberDeclarationSyntax>();
        return NamespaceDeclaration(ParseName(ns.Name))
            .AddMembers(classes);
    }

    public SyntaxNode Visit(ClassElement cls)
    {
        ClassDeclarationSyntax classDecl = ClassDeclaration(cls.Name)
            .AddModifiers(Token(PublicKeyword));
        
        if (cls.IsAbstract)
            classDecl = classDecl.AddModifiers(Token(AbstractKeyword));
            
        if (cls.IsStatic)
            classDecl = classDecl.AddModifiers(Token(StaticKeyword));
            
        if (cls.IsPartial)
            classDecl = classDecl.AddModifiers(Token(PartialKeyword));

        // Add base class and interfaces
        if (!string.IsNullOrEmpty(cls.BaseClass) || cls.Interfaces.Any())
        {
            var baseTypes = new List<BaseTypeSyntax>();
            
            if (!string.IsNullOrEmpty(cls.BaseClass))
                baseTypes.Add(SimpleBaseType(ParseTypeName(cls.BaseClass)));
                
            baseTypes.AddRange(cls.Interfaces.Select(i => SimpleBaseType(ParseTypeName(i))));
            
            classDecl = classDecl.WithBaseList(BaseList(SeparatedList(baseTypes)));
        }
        
        var members = new List<MemberDeclarationSyntax>();
        
        // Add properties
        members.AddRange(cls.Properties.Select(p => (MemberDeclarationSyntax)p.Accept(this)));
        
        // Add methods
        members.AddRange(cls.Methods.Select(m => (MemberDeclarationSyntax)m.Accept(this)));
        
        return classDecl.AddMembers(members.ToArray());
    }

    public SyntaxNode Visit(PropertyElement property)
    {
        var modifiers = new List<SyntaxToken> { Token(PublicKeyword) };
        
        if (property.IsStatic)
            modifiers.Add(Token(StaticKeyword));

        PropertyDeclarationSyntax propDecl = PropertyDeclaration(
            ParseTypeName(property.Type),
            Identifier(property.Name))
            .AddModifiers(modifiers.ToArray());

        // Add accessors
        var accessors = new List<AccessorDeclarationSyntax>();
        
        if (property.HasGetter)
        {
            var getter = property.GetterBody is not null
                ? AccessorDeclaration(GetAccessorDeclaration)
                    .WithExpressionBody(ArrowExpressionClause(ParseExpression(property.GetterBody.Code)))
                    .WithSemicolonToken(Token(SemicolonToken))
                : AccessorDeclaration(GetAccessorDeclaration)
                    .WithSemicolonToken(Token(SemicolonToken));
            accessors.Add(getter);
        }
        
        if (property.HasSetter)
        {
            var setter = AccessorDeclaration(SetAccessorDeclaration)
                .WithSemicolonToken(Token(SemicolonToken));
            accessors.Add(setter);
        }

        propDecl = propDecl.WithAccessorList(AccessorList(List(accessors)));

        // Add default value if specified
        if (property.DefaultValue is not null)
        {
            propDecl = propDecl.WithInitializer(
                EqualsValueClause(ParseExpression(property.DefaultValue.Code)))
                .WithSemicolonToken(Token(SemicolonToken));
        }

        // Add attributes
        if (property.Attributes.Any())
        {
            var attributes = property.Attributes.Select(attr =>
                AttributeList(SingletonSeparatedList(Attribute(ParseName(attr)))));
            propDecl = propDecl.WithAttributeLists(List(attributes));
        }

        return propDecl;
    }

    public SyntaxNode Visit(MethodElement method)
    {
        // 1) Build parameter list, injecting `this` on param[0] if needed
        ParameterSyntax[] parameters = method.Parameters
            .Select((p, i) =>
            {
                ParameterSyntax parm = Parameter(Identifier(p.ParamName))
                    .WithType(ParseTypeName(p.ParamType));

                if (method.IsExtensionMethod && i == 0)
                {
                    parm = parm.AddModifiers(Token(ThisKeyword));
                }

                return parm;
            })
            .ToArray();

        // 2) Start method declaration
        MethodDeclarationSyntax methodDecl = MethodDeclaration(ParseTypeName(method.ReturnType), Identifier(method.Name))
            .AddModifiers(Token(PublicKeyword), Token(StaticKeyword))
            .WithParameterList(ParameterList(SeparatedList(parameters)));

        // 3) If it's a partial declaration no body, emit “private static partial …;”
        if (method.IsPartialDeclaration)
        {
            return methodDecl
                .WithModifiers(TokenList(
                    Token(PrivateKeyword),
                    Token(StaticKeyword),
                    Token(PartialKeyword)))
                .WithSemicolonToken(Token(SemicolonToken));
        }

        // 4) Add XML documentation if provided
        if (!string.IsNullOrEmpty(method.XmlDocumentation))
        {
            // TODO: Add XML documentation support
        }

        // 5) Expression-bodied method
        if (method.ExpressionBody is { } exprBody)
        {
            ExpressionSyntax expr = ParseExpression(exprBody.Code);
            return methodDecl
                .WithExpressionBody(ArrowExpressionClause(expr))
                .WithSemicolonToken(Token(SemicolonToken));
        }

        // 6) Method body (block)
        if (method.MethodBody is { } methodBody)
        {
            BlockSyntax block = Block(ParseStatement(methodBody.Code));
            return methodDecl.WithBody(block);
        }

        // fallback (shouldn't happen here)
        return methodDecl;
    }

    public SyntaxNode Visit(ExpressionElement expr)
    {
        return ParseExpression(expr.Code);
    }
}

// === Usage Example ===
// var model = new CompilationUnitElement { ... populate model ... };
// var visitor = new SyntaxFactoryVisitor();
// var syntaxRoot = model.Accept(visitor) as CompilationUnitSyntax;
// var code = syntaxRoot?.ToFullString();
