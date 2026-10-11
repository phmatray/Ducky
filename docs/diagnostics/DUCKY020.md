# DUCKY020: Invalid `[DuckyAction]` type

**Package:** Ducky (generator `Ducky.Generators`). **Surface:** build error on the action's name (SPEC §16.1, GEN-01).

## What happened

The dispatch-helper generator can't emit `{Action}DispatchExtensions` for this type without producing code that fails
to compile, so it emits nothing for it and reports why:

- the type is generic, or nested in a generic type;
- the type is `file`-local, or it or a containing type is `private`, `protected` or `private protected`, so a class in
  its namespace can't reach it;
- the type, or a containing type, is marked `[Obsolete(..., error: true)]` (CS0619 can't be suppressed in generated
  code);
- no constructor is usable: the type is abstract, a `ref struct` or static, or every constructor is inaccessible,
  marked `[Obsolete(..., error: true)]` or takes a parameter whose type is error-obsolete. Only explicitly declared
  constructors count (the implicit parameterless one only when the type declares none), and never the record copy
  constructor. While the type or a base type has a `required` member, only a `[SetsRequiredMembers]` constructor counts
  (`new` through any other one is CS9035);
- `MethodName` is not a valid C# identifier, or it equals the generated class name (CS0542);
- the generated class name is already declared in the namespace, by a type or namespace of yours, or by another
  `[DuckyAction]` type whose joined name coincides (a nested record's class name joins its containing types with `_`:
  `A.B_C` and `A_B.C` both give `A_B_CDispatchExtensions`; both are reported).

## How to fix it

Make the type a non-generic, accessible (`public` or `internal`) record with at least one accessible constructor that
is not error-obsolete (and is `[SetsRequiredMembers]` if the type has `required` members), give `MethodName` a valid
identifier other than the class name, or rename the type or the colliding declaration. Without a helper you can still
dispatch the action directly: `store.Dispatch(new MyAction(...))`.
