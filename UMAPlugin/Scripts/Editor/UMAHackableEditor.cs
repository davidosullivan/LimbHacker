using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NobleMuffins.LimbHacker.Guts;
using UMA;
using UMA.CharacterSystem;
using UMA.Editors;

namespace NobleMuffins.LimbHacker
{
	[CustomEditor(typeof(UMAHackable))]
	public class UMAHackableEditor : Editor
	{
		private UMAHackable _target;
		private List<UMATransform> _availableTransforms = new List<UMATransform>();
		private List<UMATransform> _selectedTransforms = new List<UMATransform>();

		SerializedProperty _applyStandardUMABoneFiltersProp;

		private string[] _standardUMABoneFilters = new string[] { "Global", "Position", "Adjust" };

		public override void OnInspectorGUI()
		{
			_target = (UMAHackable)target;
			_target.Init();
			bool changed = false;

			_applyStandardUMABoneFiltersProp = serializedObject.FindProperty("_applyStandardUMABoneFilters");

			//Add the standard Hackable fields

			_target.alternatePrefab = EditorGUILayout.ObjectField(new GUIContent("Alternate prefab", "An alternative prefab to use if the character becomes a ragdoll as a result of hacking!"), (Object)_target.alternatePrefab, typeof(GameObject), false);

			_target.infillMaterial = (Material)EditorGUILayout.ObjectField(new GUIContent("Infill Material", "A material to use to fill the gaps in a mesh, might be a bloody stump kind of thing"), (Object)_target.infillMaterial, typeof(Material), false);

			if (_target.infillMaterial != null)
			{
				_target.infillMode = (InfillMode)EditorGUILayout.EnumPopup(new GUIContent("Infill Mode", "'Sloppy is more relaiable and less processor intensive, but your 'infill' texture may be distorted. 'Meticulous' will attempt to fill the top of the sliced mesh perfectly but might fail to do so"), _target.infillMode);
			}

			//TODO Add 'components to remove on Hack' if necessary
			//TODO Add 'components to add on Hack' if required

			EditorGUILayout.Space();

			//lets use our indented block for this
			GUIHelper.BeginVerticalPadded(3, new Color(0.75f, 0.875f, 1f, 0.3f));

			EditorGUILayout.HelpBox("Select which bones are severable. Use the 'Standard UMA Filters' button to remove 'Adjust' bones etc:", MessageType.Info);
			EditorGUI.indentLevel++;

			_availableTransforms = new List<UMATransform>(_target.umaSeverables);
			_selectedTransforms = new List<UMATransform>(_target.selectedUmaSeverables);

			GUILayout.BeginHorizontal();

			EditorGUI.BeginChangeCheck();
			_applyStandardUMABoneFiltersProp.boolValue = GUILayout.Toggle(_applyStandardUMABoneFiltersProp.boolValue, new GUIContent("Standard UMA Filters","Filter out standard UMA Bones like 'Global', 'Position' and the 'Adjust' bones from this list"),"Button");
			if(EditorGUI.EndChangeCheck())
			{
				serializedObject.ApplyModifiedProperties();
				_target.UpdateSeverablesFromActiveRecipe((_applyStandardUMABoneFiltersProp.boolValue ? _standardUMABoneFilters : null) );
				changed = true;
			}
			if (GUILayout.Button("Select All"))
			{
				for (int i = 0; i < _availableTransforms.Count; i++)
				{
					if (!IsUMATransformInList(_availableTransforms[i], _selectedTransforms))
						_selectedTransforms.Add(_availableTransforms[i]);
				}
				changed = true;
			}
			if (GUILayout.Button("Deselect All"))
			{
				_selectedTransforms.Clear();
				changed = true;
			}
			GUILayout.EndHorizontal();

			//Output the list of avalable bones (filtered)
			for(int i = 0; i < _availableTransforms.Count; i++)
			{
				var wasSelected = IsUMATransformInList(_availableTransforms[i],_selectedTransforms);
				var isSelected = EditorGUILayout.Toggle(_availableTransforms[i].name, wasSelected);
				//UMATransform doesn't have a System.IEquatable<UMATransform> so we have to use UMATransformComparer
				if (isSelected != wasSelected)
				{
					if (isSelected)
						_selectedTransforms.Add(_availableTransforms[i]);
					else if (!isSelected && wasSelected)
						_selectedTransforms.Remove(_availableTransforms[i]);
					changed = true;
				}
			}

			if (changed)
			{
				_target.ClearSelectedSeverables();

				for (int i = 0; i < _selectedTransforms.Count; i++)
				{
					_target.AddUMATransformToSeverables(_selectedTransforms[i]);
				}

				//do we want to try and update the associated transforms here too (i.e. if the user has used bone builder the actual severable transforms could be updated here too)
				//I think so...
				_target.ConvertUMASeverablesToSeverables();
			}

			EditorGUI.indentLevel--;
			GUIHelper.EndVerticalPadded(3);

			//base.OnInspectorGUI();
		}
		//UMATransform doesn't have a System.IEquatable<UMATransform> so we have to compare by hash
		private bool IsUMATransformInList(UMATransform umaTransformToCheck, List<UMATransform> listToCheck)
		{
			for(int i = 0; i < listToCheck.Count; i++)
			{
				if (listToCheck[i].hash == umaTransformToCheck.hash)
					return true;
			}
			return false;
		}
	}
}
