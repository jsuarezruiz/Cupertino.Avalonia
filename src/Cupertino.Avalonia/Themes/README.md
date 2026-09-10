# Theme organization

The theme follows these conventions:

- Keep one control or closely related control family per dictionary. Use a descriptive singular filename for one control and a family name such as `ButtonVariants` when a dictionary intentionally contains several related controls.
- Use `controls` for `clr-namespace:Cupertino.Controls` and `motion` for `clr-namespace:Cupertino.Animation`. Do not introduce aliases for the same namespace.
- Use an implicit `{x:Type ...}` key for the default control theme. Add a named `Cupertino...` theme only when another theme inherits from it or a template explicitly selects it. Named themes are implementation building blocks, not a parallel public key for every implicit theme.
- Use short semantic classes such as `prominent` for documented consumer variants. Prefix implementation-only state classes with `cupertino-` and use kebab-case.
- Use `DynamicResource` for appearance-dependent colors, brushes, typography tokens and other values that can change at runtime. Use `StaticResource` for invariant geometry and named theme dependencies.

`CupertinoTheme.axaml` merges dictionaries in three documented groups. Shared prerequisites come first because later dictionaries reference their named resources with `StaticResource`; the remaining dictionaries are grouped by responsibility. Keep Light and Dark palette keys and value types identical. `ThemeIntegrityTests` enforces palette parity and verifies that both variants resolve and template correctly.
