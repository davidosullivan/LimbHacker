using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UMA;
using UMA.Dynamics;
using UMA.CharacterSystem;

namespace NobleMuffins.LimbHacker
{
	//[RequireComponent(typeof(Hackable))]
	public class UMASliceHandler : AbstractSliceHandler
	{

		[Tooltip("If the UMA is inside another game object (for example a character controller) reference that here. This will ensure any sliced meshes are moved out side it so they stay in place if the character moves.")]
		public GameObject encapsulatingGameObject;

		// Use this for initialization
		void Start()
		{

		}

		// Update is called once per frame
		void Update()
		{

		}

		public override bool cloneAlternate(Dictionary<string, bool> hierarchyPresence)
		{
			return false;
		}

		public override void handleSlice(GameObject[] results)
		{
			//basically all we need is for the bones that are in use by the chopped off limb to be enabled and have their rigid bodies set to non kinematic
			//For some reason ALL the bones seem to get switched off when the clone is made- what does this?
			//The problem seems to be in LimbHackerAgent.HandleHierarchy where because of UMA bone heirarchy the Global Position and Hips bones get turned off
			//and also because the smr is weighted to bones we are not slicing (i.e. the adjust bones) we are getting the wrong results
			//so with the right leg for example
			//On the avatar
			//Disable the DCA
			//Disable the Animator (seems to need the physics avatar ENABLED and in ragdoll mode)
			//maybe disable the capsule collider- disabling the collider makes it fall through the floor
			//maybe rigid body needs to be kinematic on the dca? Not sure- ragdolled turns Kinematic off anyways
			//RigidBody NOT kinematic
			//Enable Root/Global/Position/Hips/RightUpleg+children
			//make all the rigidbodies non kinematic
			for (int i = 0; i < results.Length; i++)
			{
				if(results[i] != this.gameObject)
				{
					List<int> usedBoneIndexes = new List<int>();
					List<Transform> allAnimatedBones = new List<Transform>();
					SkinnedMeshRenderer umaSMR = null;
					//Try to find the smr
					var umaRendererGO = results[i].transform.Find("UMARenderer");
					if(umaRendererGO != null)
					{
						umaSMR = umaRendererGO.GetComponent<SkinnedMeshRenderer>();
						if(umaSMR != null)
						{
							//find the bones that are actually used by the sliced mesh
							foreach(BoneWeight bw in umaSMR.sharedMesh.boneWeights)
							{
								if (bw.boneIndex0 >= 0 && !usedBoneIndexes.Contains(bw.boneIndex0))
									usedBoneIndexes.Add(bw.boneIndex0);
								if (bw.boneIndex1 >= 0 && !usedBoneIndexes.Contains(bw.boneIndex1))
									usedBoneIndexes.Add(bw.boneIndex1);
								if (bw.boneIndex2 >= 0 && !usedBoneIndexes.Contains(bw.boneIndex2))
									usedBoneIndexes.Add(bw.boneIndex2);
								if (bw.boneIndex3 >= 0 && !usedBoneIndexes.Contains(bw.boneIndex3))
									usedBoneIndexes.Add(bw.boneIndex3);
							}
							for(int ubi = 0; ubi < usedBoneIndexes.Count; ubi++)
							{
								Debug.Log("Used Bone index[" + usedBoneIndexes[ubi] + "] was " + umaSMR.bones[usedBoneIndexes[ubi]].name);
							}
						}
					}
					//disable/destroy some components on the sliced object
					if (results[i].GetComponent<DynamicCharacterAvatar>() != null)
					{
						results[i].GetComponent<DynamicCharacterAvatar>().BuildCharacterEnabled = false;
					}
					if (results[i].GetComponent<UMAPhysicsAvatar>() != null)
					{
						DestroyImmediate(results[i].GetComponent<UMAPhysicsAvatar>());
					}
					if (results[i].GetComponent<Animator>() != null)
					{
						results[i].GetComponent<Animator>().enabled = false;
					}
					if (results[i].GetComponent<UMAHackable>() != null)
					{
						DestroyImmediate(results[i].GetComponent<UMAHackable>());
					}
					if(results[i].GetComponent<UMASliceHandler>() != null)
					{
						DestroyImmediate(results[i].GetComponent<UMASliceHandler>());
					}
					if (results[i].GetComponent<CapsuleCollider>() != null)
					{
						results[i].GetComponent<CapsuleCollider>().enabled = false;
					}
					if (results[i].GetComponent<Rigidbody>() != null)
					{
						results[i].GetComponent<Rigidbody>().isKinematic = true;
					}
					//find Root and enable it
					//find Global and enable it
					//find Position and enable it
					//for each of the bones used by the mesh
					//enable it
					//if it has a rigid body component on it set it to NOT kinematic
					//if it has a parent that is NOT Position
					//enable it
					//if the parent has a rigid body component on set it to NOT kinematic
					var root = results[i].transform.Find("Root");
					if(root != null)
					{
						root.gameObject.SetActive(true);
						var global = root.Find("Global");
						if(global != null)
						{
							global.gameObject.SetActive(true);
							var position = global.Find("Position");
							if(position != null)
							{
								position.gameObject.SetActive(true);
								var hips = position.Find("Hips");
								if (hips != null)
								{
									hips.gameObject.SetActive(true);
									if (hips.GetComponent<Rigidbody>() != null)
										hips.GetComponent<Rigidbody>().isKinematic = false;
									if (hips.GetComponent<Collider>() != null)
										hips.GetComponent<Collider>().enabled = true;

									allAnimatedBones.Add(hips);
									FindAllBonesRecursive(hips, ref allAnimatedBones);
									//We have two problems
									//1) when the limb is sliced its collider causes it to fling miles away from the body because it hits the collider of the actual uma
									//I think we may need to change the layer of the hacked object itself to be ragdoll- NOPE MAKES NO DIFFERENCE
									int ragdollLayer = 8;
									if (this.gameObject.GetComponent<UMAPhysicsAvatar>() != null)
										ragdollLayer = this.gameObject.GetComponent<UMAPhysicsAvatar>().ragdollLayer;
									results[i].layer = ragdollLayer;
									//2) somehow we need all the bones that the sliced bit is attached to to collapse BUT not get stuck in the UMA, so we need colliders enabled
									//but we need the UMAs colliders not to affect it. If we dont enable the colliders they fall through the floor and stretch the skinned mesh

									//loop over all the bones and enable them and enable any colliders and make them non-kinematic
									foreach (Transform bone in allAnimatedBones)
									{
										if (bone != root && bone != global && bone != position && bone != hips)
										{
											if (bone.GetComponent<Rigidbody>() != null)
												bone.GetComponent<Rigidbody>().isKinematic = false;
											if (bone.GetComponent<Collider>() != null)
												bone.GetComponent<Collider>().enabled = true;
											bone.gameObject.SetActive(true);
										}
									}
									//somehow we only want the bones that the sliced bit uses to be in play here
									//but the following is junk- its already done but the loop above now
									if (umaSMR != null)
									{
										for (int ubi = 0; ubi < usedBoneIndexes.Count; ubi++)
										{
											umaSMR.bones[usedBoneIndexes[ubi]].gameObject.SetActive(true);
											if (umaSMR.bones[usedBoneIndexes[ubi]].GetComponent<Rigidbody>() != null)
												umaSMR.bones[usedBoneIndexes[ubi]].GetComponent<Rigidbody>().isKinematic = false;
											if (umaSMR.bones[usedBoneIndexes[ubi]].GetComponent<Collider>() != null)
												umaSMR.bones[usedBoneIndexes[ubi]].GetComponent<Collider>().enabled = true;
											if (umaSMR.bones[usedBoneIndexes[ubi]].parent != hips && umaSMR.bones[usedBoneIndexes[ubi]] != position)
											{
												umaSMR.bones[usedBoneIndexes[ubi]].parent.gameObject.SetActive(true);
												if (umaSMR.bones[usedBoneIndexes[ubi]].parent.GetComponent<Rigidbody>() != null)
													umaSMR.bones[usedBoneIndexes[ubi]].parent.GetComponent<Rigidbody>().isKinematic = false;
												if (umaSMR.bones[usedBoneIndexes[ubi]].parent.GetComponent<Collider>() != null)
													umaSMR.bones[usedBoneIndexes[ubi]].parent.GetComponent<Collider>().enabled = true;
											}
										}
									}
								}
							}
						}
					}
					Debug.LogWarning("HackComplete");
				}
			}
		}

		private void FindAllBonesRecursive(Transform parentBone, ref List<Transform> allBones)
		{
			foreach(Transform child in parentBone.GetComponentsInChildren<Transform>())
			{
				if(child != parentBone)
				{
					if (!allBones.Contains(child))
						allBones.Add(child);
					if (child.childCount > 0)
						FindAllBonesRecursive(child, ref allBones);
				}
			}
		}
			
	}
}
