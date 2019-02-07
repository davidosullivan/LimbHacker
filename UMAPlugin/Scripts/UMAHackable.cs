using UnityEngine;
using System.Collections.Generic;
using System;
using NobleMuffins.LimbHacker.Guts;
using UMA;
using UMA.CharacterSystem;

namespace NobleMuffins.LimbHacker
{
	[RequireComponent(typeof(DynamicCharacterAvatar))]
	public class UMAHackable : Hackable
	{
		[SerializeField]
		private DynamicCharacterAvatar _characterAvatar;

		//the last known race of the UMA. When this changes the list of possible severables gets updated based on the new races 'baseRecipe'
		[SerializeField]
		private string _lastRaceName;

#pragma warning disable 0414 //this isn't used but we need the list for the editor TODO cant it just go in the editor then?

		//an edit time list of the bones that will be available when the avatar is created
		[SerializeField]
		private UMATransform[] _umaSeverables = new UMATransform[0];

#pragma warning restore 0414

		//an edit time list of the bones that have been selected
		[SerializeField]
		private UMATransform[] _selectedUmaSeverables = new UMATransform[0];

		[SerializeField]
		[Tooltip("When part of this chracter is cut off remove these components from it. Usually you want to at least remove the DCA, the ExpressionPlayer and this")]
		private List<Component> _componentsToRemoveOnHack = new List<Component>();

		[SerializeField]
		[Tooltip("When part of this chracter is cut off add these prefabs to it. These could be game objects with particle systems to pour out blood for example")]
		private List<GameObject> _prefabsToAddOnHack = new List<GameObject>();

		//used in the editor for filtering the uma bone list of the standard uma adjust bones
		[SerializeField]
		private bool _applyStandardUMABoneFilters = true;
		private string[] _standardUMABoneFilters = new string[] { "Global", "Position", "Adjust" };

		private Dictionary<string, UMATransform[]> _cachedRaceSeverables = new Dictionary<string, UMATransform[]>();

		public UMATransform[] umaSeverables
		{
			get { return _umaSeverables; }
		}

		public UMATransform[] selectedUmaSeverables
		{
			get { return _selectedUmaSeverables; }
		}

		private void OnEnable()
		{
			Init();
		}

		private void Awake()
		{
			Init();
		}

		public void Init()
		{
			if (_characterAvatar == null)
			{
				_characterAvatar = gameObject.GetComponent<DynamicCharacterAvatar>();
				//use the string here because the race itself might be in an assetBundle and we dont want to cause it to download here
				_lastRaceName = _characterAvatar.activeRace.name;
				UpdateSeverablesFromActiveRecipe((_applyStandardUMABoneFilters? _standardUMABoneFilters : null));
				ConvertUMASeverablesToSeverables();
			}
			else
			{
				CheckForRaceChange();
			}
		}

		private void Update()
		{
			CheckForRaceChange();
		}

		/// <summary>
		/// Checks if the race of the DynamicCharacterAvatar has changed, if it has it updated the list of available bones. At runtime it will also rebuild the list of severable Transforms
		/// </summary>
		private void CheckForRaceChange()
		{
			if (!string.IsNullOrEmpty(_lastRaceName) && _lastRaceName != _characterAvatar.activeRace.name)
			{
				Debug.Log("Process raceChange");
				//If the application is playing the race and its base slots might be in assetBundles and not have downloaded yet so check for that
				//in the case of an assetBundle download pending the umaData.umaRecipe.raceName wont match the active racename until DCA has everything it needs
				//and has called BuildCharacter and made the umaData dirty
				//if the umaData is dirty, wait for generator to finish making the character before we try and get the bones
				if (Application.isPlaying)
				{
					if (_characterAvatar.umaData.umaRecipe.raceData.raceName != _characterAvatar.activeRace.name || _characterAvatar.umaData.dirty)
						return;
				}
				CacheSeverablesForCurrentRace();
				UpdateSeverablesFromActiveRecipe((_applyStandardUMABoneFilters ? _standardUMABoneFilters : null));
				ConvertUMASeverablesToSeverables();
				_lastRaceName = _characterAvatar.activeRace.name;
			}
		}

		private void CacheSeverablesForCurrentRace()
		{
			if (!_cachedRaceSeverables.ContainsKey(_lastRaceName))
			{
				_cachedRaceSeverables.Add(_lastRaceName, new UMATransform[0]);
			}
			_cachedRaceSeverables[_lastRaceName] = _selectedUmaSeverables;
		}

		/// <summary>
		/// Searches the recipe of the current active race for all its slots and builds a list of available bones from the slots meshData
		/// </summary>
		public void UpdateSeverablesFromActiveRecipe(string[] filters = null)
		{
			Debug.Log("UpdateSeverablesFromActiveRecipe for " + _characterAvatar.activeRace.name);
			var umaSeverablesList = new List<UMATransform>();
			var selectedUmaSeverablesList = new List<UMATransform>();
			UMAData.UMARecipe umaRecipe = new UMAData.UMARecipe();
			_characterAvatar.activeRace.data.baseRaceRecipe.Load(umaRecipe, _characterAvatar.context);
			foreach (SlotData slot in umaRecipe.GetAllSlots())
			{
				if (slot == null || slot.asset == null || slot.asset.meshData == null || slot.asset.meshData.umaBones == null)
					continue;
				//UMATransform doesn't have a System.IEquatable<UMATransform> so we have to compare by hash
				foreach (UMATransform umaTrans in slot.asset.meshData.umaBones)
				{
					//Find out if umaSeverablesList already has this UMATransform in it
					bool canAdd = !IsUMATransformInList(umaTrans, umaSeverablesList);
					if (canAdd)
					{
						//filter out any bones we dont want
						if(filters != null)
						for (int fi = 0; fi < filters.Length; fi++)
						{
							if (!string.IsNullOrEmpty(filters[fi]) && umaTrans.name.IndexOf(filters[fi]) > -1)
								canAdd = false;
						}
						if (canAdd)
							umaSeverablesList.Add(umaTrans);
					}
				}
			}
			if (_cachedRaceSeverables.ContainsKey(_characterAvatar.activeRace.name))
			{
				//used the cached UMATransforms if we have them
				selectedUmaSeverablesList = new List<UMATransform>(_cachedRaceSeverables[_characterAvatar.activeRace.name]);
			}
			else
			{
				//only add any selected UMATrans that are in the new list
				for (int i = 0; i < _selectedUmaSeverables.Length; i++)
				{
					if(IsUMATransformInList(_selectedUmaSeverables[i], umaSeverablesList))
						selectedUmaSeverablesList.Add(_selectedUmaSeverables[i]);
				}
			}
			//can we sort _umaSeverables so it reflects the bone heirarcy? Its pretty horrible like this. Probably can since each bone contains 'parent' data
			_umaSeverables = SortUMASeverablesList(umaSeverablesList).ToArray();
			_selectedUmaSeverables = selectedUmaSeverablesList.ToArray();
		}

		/// <summary>
		/// TODO sorts a given list of UMATransforms by their parents so they result in something similar to a flattenned bone heirarchy
		/// </summary>
		/// <param name="listToSort"></param>
		/// <returns></returns>
		private List<UMATransform> SortUMASeverablesList(List<UMATransform> listToSort)
		{
			//maybe we can do a recursive loop?
			//could be heavy though, so probably only do this in the editor
			return listToSort;
		}

		/// <summary>
		/// Converts the edit time list of UMA Severables (UMATransforms) to runtime Severables (Transforms) if the character has been created
		/// </summary>
		public void ConvertUMASeverablesToSeverables()
		{
			//actually all we care about here is if the bone structure has been created- it could have been created by 'BoneBuilder' in which case this is fine to run at edit time too
			if (_characterAvatar.transform.Find("Root") == null && (_characterAvatar.umaData == null || _characterAvatar.umaData.skeleton == null))
				return;

			List<Transform> newSeverables = new List<Transform>();
			if ((_characterAvatar.umaData != null && _characterAvatar.umaData.skeleton != null))
			{
				for (int i = 0; i < _selectedUmaSeverables.Length; i++)
				{
					if (_characterAvatar.umaData.skeleton.HasBone(_selectedUmaSeverables[i].hash))
						newSeverables.Add(_characterAvatar.umaData.skeleton.GetBoneTransform(_selectedUmaSeverables[i].hash));
				}
			}
			else if (_characterAvatar.transform.Find("Root") != null)
			{
				var umaBonesDict = new Dictionary<int, Transform>();
				FindUMABonesRecursive(_characterAvatar.transform.Find("Root"), ref umaBonesDict);
				for (int i = 0; i < _selectedUmaSeverables.Length; i++)
				{
					if (umaBonesDict.ContainsKey(_selectedUmaSeverables[i].hash))
						newSeverables.Add(umaBonesDict[_selectedUmaSeverables[i].hash]);
				}
			}
			severables = newSeverables.ToArray();
			//we probably need to update something else here...
		}

		public void AddUMATransformToSeverables(UMATransform transformToAdd)
		{
			List<UMATransform> currentSeverables = new List<UMATransform>(_selectedUmaSeverables);
			if (!IsUMATransformInList(transformToAdd,currentSeverables))
			{
				currentSeverables.Add(transformToAdd);
				_selectedUmaSeverables = currentSeverables.ToArray();
			}
		}

		public void RemoveUMATransformFromSeverables(UMATransform transformToRemove)
		{
			List<UMATransform> currentSeverables = new List<UMATransform>(_selectedUmaSeverables);
			if (IsUMATransformInList(transformToRemove, currentSeverables))
			{
				currentSeverables.Remove(transformToRemove);
				_selectedUmaSeverables = currentSeverables.ToArray();
			}
		}

		public void AddTransformToSeverables(Transform transformToAdd)
		{
			List<Transform> currentSeverables = new List<Transform>(severables);
			if (!currentSeverables.Contains(transformToAdd))
			{
				currentSeverables.Add(transformToAdd);
				severables = currentSeverables.ToArray();
			}
		}

		public void RemoveTransformFromSeverables(Transform transformToRemove)
		{
			List<Transform> currentSeverables = new List<Transform>(severables);
			if (currentSeverables.Contains(transformToRemove))
			{
				currentSeverables.Remove(transformToRemove);
				severables = currentSeverables.ToArray();
			}
		}

		public void ClearSelectedSeverables()
		{
			severables = new Transform[0];
			_selectedUmaSeverables = new UMATransform[0];
		}

		//UMATransform doesn't have a System.IEquatable<UMATransform> so we have to compare by hash
		private bool IsUMATransformInList(UMATransform umaTransformToCheck, List<UMATransform> listToCheck)
		{
			for (int i = 0; i < listToCheck.Count; i++)
			{
				if (listToCheck[i].hash == umaTransformToCheck.hash)
					return true;
			}
			return false;
		}

		//we need to replicate what Hackable Start does in here when the character is first created and if the race changes
		//how did this work before? there never were any colliders on any of the UMA Bones- something must have added them?
		public void OnCharacterCreated(UMAData umaData)
		{
			ConvertUMASeverablesToSeverables();
			Collider[] childColliders = gameObject.GetComponentsInChildren<Collider>();

			foreach (Collider c in childColliders)
			{
				GameObject go = c.gameObject;

				ChildOfHackable referencer = go.GetComponent<ChildOfHackable>();

				if (referencer == null)
					referencer = go.AddComponent<ChildOfHackable>();

				referencer.parentHackable = this;
			}
		}

		private void FindUMABonesRecursive(Transform rootBone, ref Dictionary<int, Transform> avatarBonesDict, bool clearDict = false)
		{
			if (clearDict)
			{
				avatarBonesDict.Clear();
				avatarBonesDict.Add(UMAUtils.StringToHash(rootBone.name), rootBone);
			}
			foreach (Transform child in rootBone.GetComponentsInChildren<Transform>())
			{
				if (child.gameObject != rootBone.gameObject)
				{
					if (!avatarBonesDict.ContainsKey(UMAUtils.StringToHash(child.name)))
					{
						avatarBonesDict.Add(UMAUtils.StringToHash(child.name), child);
					}
					if (child.childCount > 0)
					{
						FindUMABonesRecursive(child, ref avatarBonesDict);
					}
				}
			}
		}

	}

}