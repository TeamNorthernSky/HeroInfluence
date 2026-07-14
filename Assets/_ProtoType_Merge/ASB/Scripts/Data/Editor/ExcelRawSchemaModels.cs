using System;
using System.Collections.Generic;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    public enum RawImportStatus
    {
        Imported,
        SchemaApplied,
        ValidationFailed,
        Validated,
        Generating,
        Generated,
        Failed
    }

    public enum ExcelSchemaFieldType
    {
        String,
        Int,
        Float,
        Bool,
        Percent,
        ListString,
        ListInt,
        ListFloat,
        ListBool
    }

    public enum DuplicateKeyPolicy
    {
        Error,
        KeepFirst,
        KeepLast,
        AllowList
    }

    public enum ListDelimiter
    {
        Comma,
        Backslash
    }

    [Serializable]
    public sealed class RawExcelRow
    {
        public List<string> Cells = new List<string>();
    }

    [Serializable]
    public sealed class ExcelColumnMapping
    {
        public int sourceColumnIndex;
        public string sourceHeaderName;
        public string fieldName;
        public string displayName;
        public ExcelSchemaFieldType fieldType = ExcelSchemaFieldType.String;
        public ListDelimiter listDelimiter = ListDelimiter.Comma;
        public bool include = true;
        public string defaultValue;
        public bool isKey;
    }

}
