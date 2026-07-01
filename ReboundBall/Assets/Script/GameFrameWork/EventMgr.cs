using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mr3
{
    public class EventMgr : SingletonBase<EventMgr>
    {
        //事件委托
        public delegate void OnEventAction(string eventName, object udata);
        //委托字典
        private Dictionary<string, OnEventAction> eventActions = null;

        EventMgr()
        {
            Init();
        }
        /// <summary>
        /// 初始化委托字典
        /// </summary>
        public void Init()
        {
            this.eventActions = new Dictionary<string, OnEventAction>();
        }


        /// <summary>
        /// 添加监听
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="onEvent"></param>
        public void AddListener(string eventName, OnEventAction onEvent)
        {
            if (this.eventActions.ContainsKey(eventName))
            {
                this.eventActions[eventName] += onEvent;
            }
            else
            {
                this.eventActions[eventName] = onEvent;
            }
        }

        /// <summary>
        /// 移除监听
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="onEvent"></param>
        public void RemoveListener(string eventName, OnEventAction onEvent)
        {
            if (this.eventActions != null)
            {
                if (this.eventActions.ContainsKey(eventName))
                {
                    this.eventActions[eventName] -= onEvent;
                }
            }
        }

        /// <summary>
        /// 发送事件
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="udata"></param>
        public void Emit(string eventName, object udata)
        {
            if (this.eventActions.ContainsKey(eventName))
            {
                if (this.eventActions[eventName] != null)
                {
                    this.eventActions[eventName](eventName, udata);
                }
            }
        }

        /// <summary>
        /// 释放
        /// </summary>
        public void Dispose()
        {
            if (this.eventActions != null)
            {
                this.eventActions.Clear();
                this.eventActions = null;
            }
        }
    }
}
