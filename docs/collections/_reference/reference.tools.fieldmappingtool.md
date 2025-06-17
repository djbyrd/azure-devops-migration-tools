---
optionsClassName: FieldMappingToolOptions
optionsClassFullName: MigrationTools.Tools.FieldMappingToolOptions
configurationSamples:
- name: defaults
  order: 2
  description: 
  code: There are no defaults! Check the sample for options!
  sampleFor: MigrationTools.Tools.FieldMappingToolOptions
- name: sample
  order: 1
  description: 
  code: There is no sample, but you can check the classic below for a general feel.
  sampleFor: MigrationTools.Tools.FieldMappingToolOptions
- name: classic
  order: 3
  description: 
  code: >-
    {
      "$type": "FieldMappingToolOptions",
      "Enabled": false,
      "FieldMaps": []
    }
  sampleFor: MigrationTools.Tools.FieldMappingToolOptions
description: Tool for applying field mapping transformations to work items during migration, supporting various field mapping strategies like direct mapping, regex transformations, and value lookups.
className: FieldMappingTool
typeName: Tools
architecture: 
options:
- parameterName: Enabled
  type: Boolean
  description: If set to `true` then the tool will run. Set to `false` and the processor will not run.
  defaultValue: missing XML code comments
- parameterName: FieldMaps
  type: List
  description: Gets or sets the list of field mapping configurations to apply.
  defaultValue: missing XML code comments
status: missing XML code comments
processingTarget: missing XML code comments
classFile: src/MigrationTools/Tools/FieldMappingTool.cs
optionsClassFile: src/MigrationTools/Tools/FieldMappingToolOptions.cs
notes:
  exists: false
  path: docs/Reference/Tools/FieldMappingTool-notes.md
  markdown: ''

redirectFrom:
- /Reference/Tools/FieldMappingToolOptions/
layout: reference
toc: true
permalink: /Reference/Tools/FieldMappingTool/
title: FieldMappingTool
categories:
- Tools
- 
topics:
- topic: notes
  path: docs/Reference/Tools/FieldMappingTool-notes.md
  exists: false
  markdown: ''
- topic: introduction
  path: docs/Reference/Tools/FieldMappingTool-introduction.md
  exists: false
  markdown: ''

---