/**
Copyright (c) 2014, Michael Notarnicola
All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BSGTools.Events {
	public abstract partial class MonoEvent : MonoBehaviour {
		#region Events & Delegates
		public delegate void OnAnyEventStarted(MonoEvent me);
		public static event OnAnyEventStarted AnyEventStarted;

		public delegate void OnAnyEventPaused(MonoEvent me);
		public static event OnAnyEventPaused AnyEventPaused;

		public delegate void OnAnyEventResumed(MonoEvent me);
		public static event OnAnyEventResumed AnyEventResumed;

		public delegate void OnAnyEventHalted(MonoEvent me);
		public static event OnAnyEventHalted AnyEventHalted;

		public delegate void OnAnyEventCompleted(MonoEvent me);
		public static event OnAnyEventCompleted AnyEventCompleted;

		public delegate void OnEventStarted();
		public event OnEventStarted EventStarted;

		public delegate void OnEventPaused();
		public event OnEventPaused EventPaused;

		public delegate void OnEventResumed();
		public event OnEventResumed EventResumed;

		public delegate void OnEventHalted();
		public event OnEventHalted EventHalted;

		public delegate void OnEventCompleted();
		public event OnEventCompleted EventCompleted;
		#endregion

		[SerializeField]
		internal bool executeOnStart;
		[SerializeField]
		internal bool loop;
		[SerializeField]
		internal bool destroyOnComplete;

		public EventStatus Status { get; private set; }
		public bool DoingTask { get { return ActiveTasks > 0; } }

		public int ActiveTasks { get; set; }

		private List<IEnumerator> tasks = new List<IEnumerator>();

		private static List<MonoEvent> registeredEvents = new List<MonoEvent>();

		private IEnumerator UpdateEvent() {
			int count = 0;
			while(true) {
				if(count >= tasks.Count && DoingTask == false)
					break;

				if(DoingTask == false && Status == EventStatus.Active) {
					StartCoroutine(UpdateTask(tasks[count]));
					count++;
				}
				yield return null;
			}

			CompleteEvent();
		}

		private IEnumerator UpdateTask(IEnumerator task) {
			bool hasInstruction = true;
			ActiveTasks++;
			while(true) {
				hasInstruction = task.MoveNext();

				if(Status == EventStatus.Inactive || (hasInstruction == false))
					break;

				yield return (task.Current is YieldInstruction) ? task.Current : null;
			}
			ActiveTasks--;
		}

		void Update() {
			if(Status != EventStatus.Inactive)
				return;

			bool canTrigger = TriggerEvent();
			if(canTrigger)
				ExecuteEvent();
		}

		void OnDestroy() {
			registeredEvents.Remove(this);
		}

		/// <summary>
		/// Acts as a logical trigger to execute this event.
		/// This does not block calling ExecuteEvent(),
		/// and only exists for the purpose of automatic execution
		/// based on a bool.
		/// </summary>
		/// <returns>True to execute, false otherwise.</returns>
		internal virtual bool TriggerEvent() {
			return false;
		}

		private void ResetEvent() {
			StopCoroutine(UpdateTask(null));
			StopCoroutine(UpdateEvent());

			ActiveTasks = 0;
			tasks.Clear();
			tasks.AddRange(InitEvent());
		}

		private void CompleteEvent() {
			ResetEvent();

			Status = EventStatus.Inactive;

			if(EventCompleted != null)
				EventCompleted();
			if(AnyEventCompleted != null)
				AnyEventCompleted(this);

			if(destroyOnComplete)
				Destroy(this);
			else if(loop)
				ExecuteEvent();
		}

		#region Controls
		public void ExecuteEvent() {
			if(Status != EventStatus.Inactive)
				return;

			ResetEvent();

			Status = EventStatus.Active;

			if(EventStarted != null)
				EventStarted();
			if(AnyEventStarted != null)
				AnyEventStarted(this);
			StartCoroutine(UpdateEvent());
		}

		public void PauseEvent() {
			if(Status != EventStatus.Active)
				return;

			Status = EventStatus.Paused;

			if(EventPaused != null)
				EventPaused();
			if(AnyEventPaused != null)
				AnyEventPaused(this);
		}

		public void ResumeEvent() {
			if(Status != EventStatus.Paused)
				return;

			Status = EventStatus.Active;

			if(EventResumed != null)
				EventResumed();
			if(AnyEventResumed != null)
				AnyEventResumed(this);
		}

		public void HaltEvent(bool obeyLoop = false) {
			if(Status != EventStatus.Active)
				return;

			StopAllCoroutines();
			ResetEvent();

			Status = EventStatus.Inactive;

			if(EventHalted != null)
				EventHalted();
			if(AnyEventHalted != null)
				AnyEventHalted(this);

			if(loop && obeyLoop)
				ExecuteEvent();
		}
		#endregion

		void Start() {
			registeredEvents.Add(this);
			if(executeOnStart)
				ExecuteEvent();
		}

		#region Utility/Batch Methods
		public static void PauseAllEvents() {
			foreach(var e in registeredEvents)
				e.PauseEvent();
		}

		public static void ResumeAllEvents() {
			foreach(var e in registeredEvents)
				e.ResumeEvent();
		}

		public static void ExecuteAllEvents() {
			foreach(var e in registeredEvents)
				e.ExecuteEvent();
		}

		public static void HaltAllEvents() {
			foreach(var e in registeredEvents)
				e.HaltEvent();
		}
		#endregion

		/// <summary>
		/// Passes all of the coroutines to the base class for execution.
		/// </summary>
		/// <returns></returns>
		internal abstract IEnumerator[] InitEvent();

		/// <summary>
		/// Provided for easy delays between events.
		/// </summary>
		/// <param name="time">How long to delay before continuing</param>
		/// <returns></returns>
		internal IEnumerator Delay(float time) {
			float start = Time.unscaledTime;
			while(Time.unscaledTime - start < time)
				yield return null;
		}

		public static T Create<T>(bool destroyOnComplete = true) where T : MonoEvent {
			var parent = new GameObject(string.Format("[{0}] {1}", Guid.NewGuid().ToString(), typeof(T).Name));
			var me = parent.AddComponent<T>();
			me.destroyOnComplete = destroyOnComplete;
			me.EventHalted += () => {
				if(destroyOnComplete)
					Destroy(parent);
			};
			me.EventCompleted += () => {
				if(destroyOnComplete)
					Destroy(parent);
			};
			return me;
		}

		/// <summary>
		/// Provided for easy delays between events.
		/// This delay is affected by timescale.
		/// </summary>
		/// <param name="time">How long to delay before continuing</param>
		/// <returns></returns>
		internal IEnumerator ScaledDelay(float time) {
			ActiveTasks++;
			float start = Time.time;
			while(Time.time - start < time)
				yield return null;
			ActiveTasks--;
		}

		internal IEnumerator MainThread(Action a) {
			yield return null;
			a.Invoke();
		}

		internal IEnumerator NonBlock(IEnumerator ienum) {
			StartCoroutine(ienum);
			yield return null;
		}

		public enum EventStatus {
			Inactive,
			Active,
			Paused
		}
	}
}