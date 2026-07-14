using System.Collections.Generic;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    public sealed class RawExcelSheetSO : ScriptableObject
    {
        public string sourceWorkbookGuid;
        public string sourceWorkbookPath;
        public string workbookFileName;
        public string sheetName;
        public string physicalSheetId;
        public string logicalSheetKey;
        public RawImportStatus importStatus = RawImportStatus.Imported;
        [TextArea]
        public string lastError;
        public List<RawExcelRow> Rows = new List<RawExcelRow>();
    }
}
