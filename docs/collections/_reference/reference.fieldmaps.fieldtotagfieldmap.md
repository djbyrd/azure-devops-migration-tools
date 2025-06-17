---
optionsClassName: FieldToTagFieldMapOptions
optionsClassFullName: MigrationTools.Tools.FieldToTagFieldMapOptions
configurationSamples:
- name: defaults
  order: 2
  description: 
  code: There are no defaults! Check the sample for options!
  sampleFor: MigrationTools.Tools.FieldToTagFieldMapOptions
- name: sample
  order: 1
  description: 
  code: There is no sample, but you can check the classic below for a general feel.
  sampleFor: MigrationTools.Tools.FieldToTagFieldMapOptions
- name: classic
  order: 3
  description: 
  code: >-
    {
      "$type": "FieldToTagFieldMapOptions",
      "sourceField": null,
      "formatExpression": null,
      "ApplyTo": []
    }
  sampleFor: MigrationTools.Tools.FieldToTagFieldMapOptions
description: missing XML code comments
className: FieldToTagFieldMap
typeName: FieldMaps
architecture: 
options:
- parameterName: ApplyTo
  type: List
  description: A list of Work Item Types that this Field Map will apply to. If the list is empty it will apply to all Work Item Types. You can use "*" to apply to all Work Item Types.
  defaultValue: missing XML code comments
- parameterName: formatExpression
  type: String
  description: missing XML code comments
  defaultValue: missing XML code comments
- parameterName: sourceField
  type: String
  description: missing XML code comments
  defaultValue: missing XML code comments
status: missing XML code comments
processingTarget: missing XML code comments
classFile: src/MigrationTools.Clients.TfsObjectModel/Tools/FieldMappingTool/FieldMaps/FieldToTagFieldMap.cs
optionsClassFile: ''
notes:
  exists: false
  path: docs/Reference/FieldMaps/FieldToTagFieldMap-notes.md
  markdown: ''

redirectFrom:
- /Reference/FieldMaps/FieldToTagFieldMapOptions/
layout: reference
toc: true
permalink: /Reference/FieldMaps/FieldToTagFieldMap/
title: FieldToTagFieldMap
categories:
- FieldMaps
- 
topics:
- topic: notes
  path: docs/Reference/FieldMaps/FieldToTagFieldMap-notes.md
  exists: false
  markdown: ''
- topic: introduction
  path: docs/Reference/FieldMaps/FieldToTagFieldMap-introduction.md
  exists: false
  markdown: ''

---