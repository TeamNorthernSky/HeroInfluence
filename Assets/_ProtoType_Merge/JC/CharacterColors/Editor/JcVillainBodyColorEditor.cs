using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.CharacterColors.Editor
{
    [InitializeOnLoad]
    static class JcVillainBodyColorBootstrap
    {
        static double next;
        static JcVillainBodyColorBootstrap(){EditorApplication.update+=Update;}
        static void Update()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+1;
            for(int i=0;i<SceneManager.sceneCount;i++)
            {var scene=SceneManager.GetSceneAt(i);if(!EditorSceneManager.IsPreviewScene(scene))JcVillainBodyColorController.Ensure(scene);}
        }
    }

    public static class JcVillainBodyColorStorage
    {
        public static void Save(JcVillainBodyColorController c)
        {
            if(!c.profile || !EditorUtility.IsPersistent(c.profile))throw new InvalidOperationException("저장할 기본 프로필이 없습니다.");
            Undo.RecordObject(c.profile,"빌런 몸체 색상 프로필 저장");
            string name=c.profile.name;EditorUtility.CopySerialized(c.Settings,c.profile);
            c.profile.name=name;c.profile.hideFlags=HideFlags.None;
            EditorUtility.SetDirty(c.profile);AssetDatabase.SaveAssetIfDirty(c.profile);
        }
        public static Texture2D Bake(JcVillainBodyColorController c,string path)
        {
            const string folder="Assets/_ProtoType_Merge/JC/CharacterColors/Baked/";
            path=path.Replace('\\','/');
            if(!path.StartsWith(folder,StringComparison.Ordinal) || !path.EndsWith(".png",StringComparison.OrdinalIgnoreCase) || File.Exists(path))
                throw new InvalidOperationException("전용 Baked 폴더의 새 PNG 경로가 필요합니다. 원본과 기존 결과는 덮어쓰지 않습니다.");
            var p=c.Settings;
            if(!p || !p.source)throw new InvalidOperationException("원본 텍스처가 없습니다.");
            // JC | 2026-11-04 | 기본색만 별도 PNG로 저장하고 재질 슬라이더는 실시간 조명에 반응하도록 프로필에 보관합니다.
            var rt=c.RenderForBake();var prev=RenderTexture.active;
            var copy=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false,false);
            try
            {
                RenderTexture.active=rt;copy.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);copy.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,copy.EncodeToPNG());
            }
            finally{RenderTexture.active=prev;UnityEngine.Object.DestroyImmediate(copy);}
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;
            importer.maxTextureSize=Mathf.Max(rt.width,rt.height);importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.wrapMode=p.source.wrapMode;importer.filterMode=p.source.filterMode;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.SaveAndReimport();
            p.bakedTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            p.bakedTuning=JsonUtility.ToJson(p.tuning);p.useBaked=true;
            Save(c);c.ApplyNow();return p.bakedTexture;
        }
    }

    [CustomEditor(typeof(JcVillainBodyColorController))]
    sealed class JcVillainBodyColorInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var c=(JcVillainBodyColorController)target;var p=c.Settings;
            EditorGUILayout.HelpBox("소총수·방패병·필드 빌런의 몸체 중 어두운 저채도 영역만 보정합니다. 무기·아놀드·보스전은 제외합니다.\n임시 조정값은 플레이 종료·씬 종료·재컴파일 시 사라질 수 있습니다. 종료 전에 프로필 저장 또는 텍스처 베이크 버튼을 누르세요.",MessageType.Info);
            if(!p){EditorGUILayout.HelpBox("기본 몸체 색상 프로필을 찾을 수 없습니다.",MessageType.Error);return;}
            EditorGUILayout.LabelField("저장 대상",AssetDatabase.GetAssetPath(c.profile));
            var so=new SerializedObject(p);so.Update();
            var tuning=so.FindProperty("tuning");
            string[] names={"tint","strength","blackThreshold","feather","saturationLimit","sourceLightness","detail"};
            string[] labels={"보정 기준색","색상 적용 강도","검은 영역 밝기 상한","경계 부드러움","선택 채도 상한","원본 기준 명도","명암·옷감 보존"};
            for(int i=0;i<names.Length;i++){var field=tuning.FindPropertyRelative(names[i]);EditorGUILayout.PropertyField(field,new GUIContent(labels[i],field.tooltip));}
            EditorGUILayout.Space();EditorGUILayout.LabelField("검은 몸체 재질 반응",EditorStyles.boldLabel);
            // JC | 2026-11-04 | 저장 대상과 분리된 조절 복사본을 편집하므로 슬라이더를 움직이는 동안 공유 프로필을 덮지 않습니다.
            var surface=so.FindProperty("surface");
            foreach(var pair in new[]{new[]{"enabled","재질 조절 사용"},new[]{"reflection","반사광 강도"},new[]{"smoothness","광택 · 매끄러움"},new[]{"metallic","메탈릭"}}){
                var field=surface.FindPropertyRelative(pair[0]);EditorGUILayout.PropertyField(field,new GUIContent(pair[1],field.tooltip));
            }
            EditorGUILayout.HelpBox("반사광 1은 기본 강도, 광택 0은 거칠고 넓은 반사, 1은 선명한 반사입니다. 메탈릭 1은 금속입니다.\n원본 검은 영역에만 적용하며, 재질 조절값은 프로필에 저장합니다. 기본색 PNG를 다시 베이크할 필요는 없습니다.",MessageType.None);
            var baked=so.FindProperty("useBaked");EditorGUILayout.PropertyField(baked,new GUIContent("일치하는 베이크 사용",baked.tooltip));
            if(so.ApplyModifiedProperties())c.ApplyNow();
            EditorGUI.BeginChangeCheck();
            bool original=EditorGUILayout.Toggle(new GUIContent("원본 비교","원본 텍스처로 비교합니다. 조절값과 프로필은 유지합니다."),c.showOriginal);
            bool mask=EditorGUILayout.Toggle(new GUIContent("검은 영역 보기","선택 영역을 분홍색으로 확인합니다. 색상 베이크에는 포함하지 않습니다."),c.showMask);
            if(EditorGUI.EndChangeCheck()){c.showOriginal=original;c.showMask=mask;c.ApplyNow();}
            EditorGUILayout.LabelField("대상 몸체",c.TargetCount.ToString());EditorGUILayout.HelpBox(c.Status??"대상 확인 중",MessageType.None);
            EditorGUILayout.LabelField(JsonUtility.ToJson(p.tuning)==JsonUtility.ToJson(c.profile.tuning)&&JsonUtility.ToJson(p.surface)==JsonUtility.ToJson(c.profile.surface)&&p.useBaked==c.profile.useBaked?"조절값이 저장 프로필과 동일합니다.":"저장하지 않은 조절값이 있습니다.",EditorStyles.miniLabel);
            if(GUILayout.Button(new GUIContent("현재 값을 프로필에 저장","현재 임시 색상값을 기본 공유 프로필에 저장합니다. 이후 열리는 탐사·튜토리얼·일반 전투에 공통 적용합니다. 씬과 원본 텍스처는 저장하지 않습니다.")))JcVillainBodyColorStorage.Save(c);
            if(GUILayout.Button(new GUIContent("저장값 다시 적용","저장 프로필을 다시 불러옵니다. 현재 저장하지 않은 조절값은 사라집니다."))){c.Reload();c.ApplyNow();}
            if(GUILayout.Button(new GUIContent("보정 텍스처 베이크 · 프로필 저장","현재 임포트 해상도의 기본색 PNG를 새 파일로 저장하고 프로필까지 저장합니다. 조명·그림자·마스크 보기·원본 비교는 베이크하지 않습니다. 재실행 후에도 베이크 연결이 유지됩니다.")))
            {
                string folder="Assets/_ProtoType_Merge/JC/CharacterColors/Baked";
                string path=AssetDatabase.GenerateUniqueAssetPath(folder+"/JC_VillainBody_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".png");
                try{JcVillainBodyColorStorage.Bake(c,path);}catch(Exception e){Debug.LogException(e,c);}
            }
            if(p.bakedTexture)EditorGUILayout.ObjectField(new GUIContent("저장된 베이크","현재 프로필의 보정 텍스처입니다. 값이 일치할 때만 사용하며 원본 텍스처는 유지합니다."),p.bakedTexture,typeof(Texture2D),false);
        }
    }

    [CustomEditor(typeof(JcVillainBodyProfile))]
    sealed class JcVillainBodyProfileInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI(){EditorGUILayout.HelpBox("씬의 JC_Environment → JC_VillainBodyColor에서 조절하세요. 전투씬에는 JC_VillainBodyColor가 루트에 생성됩니다. 이 에셋은 공용 저장소입니다.",MessageType.Info);using(new EditorGUI.DisabledScope(true))DrawDefaultInspector();}
    }
}
