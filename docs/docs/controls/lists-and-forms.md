---
title: Lists and forms
description: Compose iOS-style grouped sections, list cells, and form rows.
ms.date: 2026-08-27
---

# Lists and forms

Use `Section` to group content, `CupertinoListCell` for list rows, and `CupertinoFormRow` for labelled inputs.

<img src="../../images/list-and-search.png"
     alt="Grouped lists and search results"
     width="420" />

```xml
<cupertino:Section Header="ACCOUNT"
                   Footer="Changes sync across your devices.">
  <StackPanel>
    <cupertino:CupertinoListCell Title="Profile"
                                Subtitle="Photo, name and contact details"
                                AccessoryKind="Disclosure" />
    <Rectangle Classes="hair" />
    <cupertino:CupertinoListCell Title="Sign out"
                                IsDestructive="True"
                                ShowsSeparator="False" />
  </StackPanel>
</cupertino:Section>
```

For editable forms, keep the label, control, help text, required state, and validation error together:

```xml
<cupertino:CupertinoFormRow Label="Email"
                           IsRequired="True"
                           HelpText="Used for account recovery"
                           ErrorText="{Binding EmailError}">
  <TextBox Text="{Binding Email}" />
</cupertino:CupertinoFormRow>
```

Use `ListBox` when you need selection, virtualization or keyboard navigation. The theme gives it matching row styles.
