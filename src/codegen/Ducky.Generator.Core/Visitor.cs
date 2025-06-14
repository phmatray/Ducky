using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Ducky.Generator.Core;

// === Model Interfaces ===
/// <summary>
/// Represents a code element that can be visited by a syntax visitor.
/// </summary>
public interface ICodeElement
{
    /// <summary>
    /// Accepts a visitor to process this code element.
    /// </summary>
    /// <typeparam name="T">The type of result returned by the visitor.</typeparam>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>The result of the visitor's processing.</returns>
    T Accept<T>(ISyntaxVisitor<T> visitor);
}

/// <summary>
/// Defines a visitor pattern for traversing and transforming code element models.
/// </summary>
/// <typeparam name="T">The type of result produced by the visitor.</typeparam>
public interface ISyntaxVisitor<T>
{
    /// <summary>
    /// Visits a compilation unit element.
    /// </summary>
    /// <param name="unit">The compilation unit to visit.</param>
    /// <returns>The result of visiting the compilation unit.</returns>
    T Visit(CompilationUnitElement unit);
    
    /// <summary>
    /// Visits a namespace element.
    /// </summary>
    /// <param name="ns">The namespace to visit.</param>
    /// <returns>The result of visiting the namespace.</returns>
    T Visit(NamespaceElement ns);
    
    /// <summary>
    /// Visits a class element.
    /// </summary>
    /// <param name="cls">The class to visit.</param>
    /// <returns>The result of visiting the class.</returns>
    T Visit(ClassElement cls);
    
    /// <summary>
    /// Visits a property element.
    /// </summary>
    /// <param name="property">The property to visit.</param>
    /// <returns>The result of visiting the property.</returns>
    T Visit(PropertyElement property);
    
    /// <summary>
    /// Visits a method element.
    /// </summary>
    /// <param name="method">The method to visit.</param>
    /// <returns>The result of visiting the method.</returns>
    T Visit(MethodElement method);
    
    /// <summary>
    /// Visits an expression element.
    /// </summary>
    /// <param name="expr">The expression to visit.</param>
    /// <returns>The result of visiting the expression.</returns>
    T Visit(ExpressionElement expr);
}

// === Model Classes ===
/// <summary>
/// Represents a compilation unit containing using directives and namespaces.
/// </summary>
public class CompilationUnitElement : ICodeElement
{
    /// <summary>
    /// Gets or sets the using directives for the compilation unit.
    /// </summary>
    public IEnumerable<string> Usings { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Gets or sets the namespaces in the compilation unit.
    /// </summary>
    public IEnumerable<NamespaceElement> Namespaces { get; set; } = Array.Empty<NamespaceElement>();

    /// <inheritdoc/>
    public T Accept<T>(ISyntaxVisitor<T> visitor)
        => visitor.Visit(this);
}

/// <summary>
/// Represents a namespace containing class declarations.
/// </summary>
public class NamespaceElement : ICodeElement
{
    /// <summary>
    /// Gets or sets the name of the namespace.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the classes within the namespace.
    /// </summary>
    public IEnumerable<ClassElement> Classes { get; set; } = Array.Empty<ClassElement>();

    /// <inheritdoc/>
    public T Accept<T>(ISyntaxVisitor<T> visitor)
        => visitor.Visit(this);
}

/// <summary>
/// Represents a class declaration with its members.
/// </summary>
public class ClassElement : ICodeElement
{
    /// <summary>
    /// Gets or sets the name of the class.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets a value indicating whether the class is static.
    /// </summary>
    public bool IsStatic { get; set; } = true;
    
    /// <summary>
    /// Gets or sets a value indicating whether the class is partial.
    /// </summary>
    public bool IsPartial { get; set; } = false;
    
    /// <summary>
    /// Gets or sets a value indicating whether the class is abstract.
    /// </summary>
    public bool IsAbstract { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the base class name.
    /// </summary>
    public string BaseClass { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the interfaces implemented by the class.
    /// </summary>
    public IEnumerable<string> Interfaces { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Gets or sets the properties of the class.
    /// </summary>
    public IEnumerable<PropertyElement> Properties { get; set; } = Array.Empty<PropertyElement>();
    
    /// <summary>
    /// Gets or sets the methods of the class.
    /// </summary>
    public IEnumerable<MethodElement> Methods { get; set; } = Array.Empty<MethodElement>();

    /// <inheritdoc/>
    public T Accept<T>(ISyntaxVisitor<T> visitor)
        => visitor.Visit(this);
}

/// <summary>
/// Represents a property declaration.
/// </summary>
public class PropertyElement : ICodeElement
{
    /// <summary>
    /// Gets or sets the name of the property.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the type of the property.
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the accessibility level of the property.
    /// </summary>
    public string Accessibility { get; set; } = "public";
    
    /// <summary>
    /// Gets or sets a value indicating whether the property has a getter.
    /// </summary>
    public bool HasGetter { get; set; } = true;
    
    /// <summary>
    /// Gets or sets a value indicating whether the property has a setter.
    /// </summary>
    public bool HasSetter { get; set; } = true;
    
    /// <summary>
    /// Gets or sets a value indicating whether the property is static.
    /// </summary>
    public bool IsStatic { get; set; } = false;
    
    /// <summary>
    /// Gets or sets the expression body for the getter.
    /// </summary>
    public ExpressionElement? GetterBody { get; set; }
    
    /// <summary>
    /// Gets or sets the default value expression.
    /// </summary>
    public ExpressionElement? DefaultValue { get; set; }
    
    /// <summary>
    /// Gets or sets the attributes applied to the property.
    /// </summary>
    public IEnumerable<string> Attributes { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Gets or sets the XML documentation for the property.
    /// </summary>
    public string XmlDocumentation { get; set; } = string.Empty;

    /// <inheritdoc/>
    public T Accept<T>(ISyntaxVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a method declaration.
/// </summary>
public class MethodElement : ICodeElement
{
    /// <summary>
    /// Gets or sets the name of the method.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the return type of the method.
    /// </summary>
    public string ReturnType { get; set; } = "void";

    /// <summary>
    /// If true, the first parameter will get a `this` modifier.
    /// </summary>
    public bool IsExtensionMethod { get; set; }

    /// <summary>
    /// If true and no body is supplied, we'll emit a `private static partial …;` declaration.
    /// </summary>
    public bool IsPartialDeclaration { get; set; }

    /// <summary>
    /// Gets or sets the ordered list of method parameters.
    /// </summary>
    public IEnumerable<ParameterDescriptor> Parameters { get; set; } = Array.Empty<ParameterDescriptor>();

    /// <summary>
    /// Gets or sets the expression body for expression-bodied methods.
    /// </summary>
    public ExpressionElement? ExpressionBody { get; set; }

    /// <summary>
    /// Gets or sets the XML documentation comments for the method.
    /// </summary>
    public string XmlDocumentation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the method body for non-expression-bodied methods.
    /// </summary>
    public ExpressionElement? MethodBody { get; set; }

    /// <inheritdoc/>
    public T Accept<T>(ISyntaxVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a code expression.
/// </summary>
public class ExpressionElement : ICodeElement
{
    /// <summary>
    /// Gets or sets the raw code string for the expression.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <inheritdoc/>
    public T Accept<T>(ISyntaxVisitor<T> visitor)
        => visitor.Visit(this);
}

// === Concrete Visitor Implementation ===
/// <summary>
/// Concrete implementation of ISyntaxVisitor that transforms code elements into Roslyn syntax nodes.
/// </summary>
public class SyntaxFactoryVisitor : ISyntaxVisitor<SyntaxNode>
{
    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public SyntaxNode Visit(NamespaceElement ns)
    {
        MemberDeclarationSyntax[] classes = ns.Classes
            .Select(c => (ClassDeclarationSyntax)c.Accept(this))
            .ToArray<MemberDeclarationSyntax>();
        return NamespaceDeclaration(ParseName(ns.Name))
            .AddMembers(classes);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
