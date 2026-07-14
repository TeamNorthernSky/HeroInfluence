using System.Collections.Generic;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    public sealed class ExcelSheetSchemaSO : ScriptableObject
    {
        public string schemaGuid;
        public string sourceWorkbookGuid;
        public string sourceWorkbookPath;
        public string workbookFileName;
        public string sourceSheetName;
        public string physicalSheetId;
        public string logicalSheetKey;
        public string templateClassName;
        public string templateSignature;
        public string className;
        public string outputAssetName;
        public string targetTableAssetGuid;
        public string targetTableAssetPath;
        public int headerRowIndex;
        public int dataStartRowIndex = 1;
        public bool useDictionary;
        public bool requireUniqueKey;
        public DuplicateKeyPolicy duplicateKeyPolicy = DuplicateKeyPolicy.Error;
        public List<ExcelColumnMapping> Columns = new List<ExcelColumnMapping>();
    }
}
