# DUCKY901: Mutty referenced next to Ducky.Draft

**Package:** Ducky.Draft (`buildTransitive/Ducky.Draft.targets`). **Surface:** MSBuild warning before compilation
(SPEC §3, §16.2).

## What happened

The project references both `Mutty` and `Ducky.Draft`. Ducky.Draft is Mutty's successor (immutable records edited
through generated drafts), so the Mutty reference is left over from before the migration.

## How to fix it

Remove the `Mutty` `PackageReference` and move the code to Ducky.Draft. The Mutty migration guide
(`docs/migration/mutty.md`) maps each Mutty attribute and API to its Ducky.Draft equivalent.
