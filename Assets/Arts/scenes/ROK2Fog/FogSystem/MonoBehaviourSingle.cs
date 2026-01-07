
using System;
using System.Collections.Generic;
using UnityEngine;

	public interface iMonoBehaviourSingleBase
	{ 
		void Clear();
	}

	


	public abstract class MonoBehaviourSingle<T> :  MonoBehaviour ,iMonoBehaviourSingleBase where T : MonoBehaviour
	{
		
		protected static T s_instance;

        protected Transform m_Tran;

		protected Transform tran
		{
			get
			{
				if (m_Tran == null)
				{
					m_Tran = gameObject.transform;
				}

				return m_Tran;
			}
		}

		public static T GetInstance()
		{
			if (s_instance == null)
			{
//				try
//				{
					GameObject gObj = new GameObject(typeof(T).Name);
					GameObject.DontDestroyOnLoad(gObj);

					Transform tran = gObj.transform;
					tran.localPosition = Vector3.zero;
					tran.localEulerAngles = Vector3.zero;
					tran.localScale = Vector3.one;

					s_instance = gObj.AddComponent<T>();
					
					
//				}
//				catch (Exception e)
//				{
//					Debug.LogError(e.ToString());
//					throw;
//				}
			}
			return s_instance;
		}

		public static bool CheckInstance()
		{
			return s_instance != null;
		}

		public static T instance
		{
			get
			{
				return GetInstance();
			}
		}
 

		private void Awake()
        {
            OnInit();
        }

        private void Update()
        {
            OnUpdate(Time.deltaTime);
        }

        protected virtual void OnInit() { }
        protected virtual void OnUpdate(float deltaTime) { }
        public virtual void Clear() { }

        public void Destroy()
        {
	        Clear(); 
	        Destroy(this.gameObject);
        }
	}
