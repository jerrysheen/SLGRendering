using UnityEngine;
using System.Collections.Generic;

public class ShadowEdgeManager : MonoBehaviour
{
	public Transform player;
	public bool disableWhenInside = true;

	List<OccluderShadowEdges> _occluders = new List<OccluderShadowEdges>(256);

	void Awake()
	{
		Refresh();
	}

	[ContextMenu("Refresh Occluders")]
	public void Refresh()
	{
		_occluders.Clear();
		_occluders.AddRange(FindObjectsOfType<OccluderShadowEdges>());
	}

	void LateUpdate()
	{
		if (!player) return;
		Shader.SetGlobalVector("_PlayerPos", player.position);
		Shader.SetGlobalFloat("_ExtrudeDist", 50.04f);
		
		Vector3 p = player.position;

		// 这一步极轻：每个 occluder 4 次 dot + enable/disable
		for (int i = 0; i < _occluders.Count; i++)
		{
			var o = _occluders[i];
			if (o) o.UpdateEdgeVisibility(p, disableWhenInside);
		}
	}
}