using UnityEngine;
namespace JC.Environment {
 [DisallowMultipleComponent,AddComponentMenu("")]
 public sealed class JcDayNightSegment : MonoBehaviour {
  [HideInInspector] public JcDayNightController controller;
  [HideInInspector] public int index;
 }
}
