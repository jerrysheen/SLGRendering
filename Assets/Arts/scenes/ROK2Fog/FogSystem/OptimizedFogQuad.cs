using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

// 用Array来模拟四叉树， 用Stack来模拟递归搜索。
public class OptimizedFogQuad
{
	public struct BoundsAABB
	{
		public Vector2Int MinXY, MaxXY;

		public BoundsAABB(Vector2Int min, Vector2Int max)
		{
			MinXY = min;
			MaxXY = max;
		}
	}

	private byte[] _isLeafArray;
	private byte[] _isExitsArray;
	private byte[] _nodeTypeArray;
	private byte[] _isDirtyArray;
	private Stack<int> _stack;
	private int _minQuadSize;
	private int _maxQuadSize;
	public OptimizedFogQuad(int maxQuadSize, int minQuadSize)
	{
		// 计算总共需要的节点数量， 400 ~ 25四等分，预先生成
		int depth = (int)Mathf.Log(maxQuadSize / minQuadSize, 2);
		int arraySize = (int)(Mathf.Pow(4,depth + 1 ) - 1) / (4 - 1);
		_minQuadSize = minQuadSize;
		_maxQuadSize = maxQuadSize;
		_isLeafArray = new byte[arraySize];
		_nodeTypeArray = new byte[arraySize];
		_isDirtyArray = new byte[arraySize];
		_isExitsArray = new byte[arraySize];
		_stack = new Stack<int>(arraySize);
		_isExitsArray[0] = 1;
		_isLeafArray[0] = 1;
	}

	// node type :  1 lock, 2 unlock, 3 partially unlock
	public void Insert(BoundsAABB aabb, byte nodeType )
	{
		if(_stack == null) _stack = new Stack<int>();
		_stack.Clear();
		_stack.Push(0);
		while (_stack.Count > 0)
		{
			 int index = _stack.Pop();
			 
			 if(index >= _isExitsArray.Length) continue;
			 // 节点和aabb无交点，返回
			 if (!IsInBound(aabb, index))
			 {
				 continue;
			 }
			 // 节点和aabb完全覆盖，设置为叶子节点，merge原来的子节点
			 if (IsOverLapping(aabb, index))
			 {
				 _nodeTypeArray[index] = nodeType;
				 _isLeafArray[index] = 1;
				 _isDirtyArray[index] = 1;
				 Delete(index * 4 + 1);
				 Delete(index * 4 + 2);
				 Delete(index * 4 + 3);
				 Delete(index * 4 + 4);
				 continue;
			 }

			 // 已经在最大深度，标记为partially unlcok即可。
			 if (index * 4 + 1 >= _isLeafArray.Length)
			 {
				 if (_nodeTypeArray[index] != nodeType)
				 {
					 _nodeTypeArray[index] = 3;
					 _isDirtyArray[index] = 1;
				 }
				 continue;
			 }
			 
			 // 判断当前是否为LeafNode, Leafnode接收到一个同样的lock/unlock不会改变逻辑
			 if (_isLeafArray[index] == 1)
			 {
				 if (_nodeTypeArray[index] == nodeType) continue;
			 }

			 SubDivide(index, _nodeTypeArray[index]);
			 _isLeafArray[index] = 0;
			 _isDirtyArray[index] = 1;
			 _stack.Push(index * 4 + 1);
			 _stack.Push(index * 4 + 2);
			 _stack.Push(index * 4 + 3);
			 _stack.Push(index * 4 + 4);
			 
		}
	}

	public void SubDivide(int index, byte nodeType)
	{
		if (index * 4 + 1 >= _isExitsArray.Length) return;
		for (int i = 1; i <= 4; i++)
		{
			int nextNode = index * 4 + i;
			if (_isExitsArray[nextNode] == 0)
			{
				_nodeTypeArray[nextNode] = nodeType;
				_isExitsArray[nextNode] = 1;
				_isLeafArray[nextNode] = 1;
				_isDirtyArray[nextNode] = 1;
			}
		}
	}

	public void Delete(int index)
	{
		if (index >= _isLeafArray.Length || _isExitsArray[index] == 0) return;
		if(_stack == null) _stack = new Stack<int>();
		int currentInstackCount = _stack.Count;
		_stack.Push(index);
		while (_stack.Count > currentInstackCount)
		{
			int currentIndex = _stack.Pop();
			if(currentIndex >= _isLeafArray.Length || _isExitsArray[currentIndex] == 0) continue;
			_isLeafArray[currentIndex] = 0;
			_nodeTypeArray[currentIndex] = 0;
			_isExitsArray[currentIndex] = 0;
			_stack.Push(currentIndex * 4 + 1);
			_stack.Push(currentIndex * 4 + 2);
			_stack.Push(currentIndex * 4 + 3);
			_stack.Push(currentIndex * 4 + 4);
		}
	}

	public void Destroy()
	{
		_isLeafArray = null;
		_isDirtyArray = null;
		_nodeTypeArray = null;
		_isExitsArray = null;
	}

	public void GetNodeBounds(int index, out Vector2Int startPos, out Vector2Int endPos)
	{
		// 得到本层level
		int level = (int)Mathf.Floor(Mathf.Log(index * 3 + 1, 4));
		// 减去上一次层
		index -= (int)(Mathf.Pow(4,level - 1 + 1) - 1) / (4 - 1);
		// 莫顿码编码， 左下右下左上右上，0123，4567，891011 
		// -> 0000 0001 0010 0011, 0100 0101 0110 0111,
		// 比如4的层内xy,为奇数IDx(10) 偶数IDy(00),则4的组内坐标为(2,0)
		int ix = 0, iy = 0;
		for (int t = 0; t < level; ++t) {
			int d = (index >> (2*t)) & 3;   // 取第 t 位的四进制数字
			ix |= (d & 1) << t;              // 取 x 的那一位
			iy |= (d >> 1) << t;             // 取 y 的那一位
		}
		// 这个地方，简单细分，确保都是整数。
		int size = _maxQuadSize / (int)Mathf.Pow(2, level);
		startPos = new Vector2Int(ix, iy);
		startPos *= size;
		endPos = new Vector2Int(ix + 1, iy + 1);
		endPos *= size;
		// 移除频繁日志，避免场景绘制时刷屏
		// Debug.Log("start end pos:" + startPos + " , " + endPos);

	}


	public bool IsInBound(BoundsAABB aabb, int nodeIndex)
	{
		// 先找到nodeIndex 所在位置， center, extend;
		Vector2Int startPos, endPos;
		GetNodeBounds(nodeIndex, out startPos, out endPos);
		return aabb.MinXY.x <= endPos.x &&
				aabb.MaxXY.x >= startPos.x &&
				aabb.MinXY.y <= endPos.y &&
				aabb.MaxXY.y >= startPos.y;
	}
	
	public bool IsOverLapping(BoundsAABB boundsAABB, int nodeIndex)
	{
		Vector2Int startPos , endPos;
		GetNodeBounds(nodeIndex, out startPos, out endPos);
		return boundsAABB.MinXY.x <= startPos.x && boundsAABB.MaxXY.x >= endPos.x &&
			   boundsAABB.MinXY.y <= startPos.y && boundsAABB.MaxXY.y >= endPos.y; 
	}

	// 公开访问器，供编辑器可视化与统计
	public int NodeCapacity => _isExitsArray != null ? _isExitsArray.Length : 0;
	public bool Exists(int index) => _isExitsArray[index] != 0;
	public bool IsLeaf(int index) => _isLeafArray[index] != 0;
	public bool IsDirty(int index) => _isDirtyArray[index] != 0;

	public void MarkRebuilt(int index)
	{
		_isDirtyArray[index] = 0;
	}

	public byte GetNodeType(int index) => _nodeTypeArray[index];
}
