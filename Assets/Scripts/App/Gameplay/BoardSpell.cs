// Copyright (c) 2018 - Loom Network. All rights reserved.
// https://loomx.io/



using System;
using UnityEngine;

namespace LoomNetwork.CZB
{
    public class BoardSpell
    {
        public event Action SpellOnUsedEvent;

        private OnBehaviourHandler _eventHandler;

        public GameObject gameObject;
        public Transform transform;

        public BoardArrow targetingArrow;

        public WorkingCard Card;

        public BoardSpell(GameObject obj, WorkingCard card)
        {
            gameObject = obj;
            transform = obj.transform;

            Card = card;

            _eventHandler = gameObject.GetComponent<OnBehaviourHandler>();

            _eventHandler.OnDestroyEvent += OnDestroyEventHandler;
        }

        private void OnDestroyEventHandler(GameObject obj)
        {
            if (targetingArrow != null)
            {
                MonoBehaviour.Destroy(targetingArrow.gameObject);
                targetingArrow = null;
            }

            SpellOnUsedEvent?.Invoke();
        }
    }
}