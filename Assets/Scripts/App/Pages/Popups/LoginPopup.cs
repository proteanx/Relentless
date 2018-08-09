// Copyright (c) 2018 - Loom Network. All rights reserved.
// https://loomx.io/



using LoomNetwork.CZB.Common;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LoomNetwork.CZB
{
    public class LoginPopup : IUIPopup
    {
        public GameObject Self
        {
            get { return _selfPage; }
        }

        public static Action OnHidePopupEvent;

        private ILoadObjectsManager _loadObjectsManager;
        private IUIManager _uiManager;
        private GameObject _selfPage;
	    private IDataManager _dataManager;

        private ButtonShiftingContent _loginButton;
		private ButtonShiftingContent _betaButton;
		private ButtonShiftingContent _waitingButton;

		private InputField _betaInput;

		private string _state;
		private float _time;

        public void Init()
        {
            _loadObjectsManager = GameClient.Get<ILoadObjectsManager>();
            _uiManager = GameClient.Get<IUIManager>();
	        _dataManager = GameClient.Get<IDataManager>();

            _selfPage = MonoBehaviour.Instantiate(_loadObjectsManager.GetObjectByPath<GameObject>("Prefabs/UI/Popups/LoginPopup"));
            _selfPage.transform.SetParent(_uiManager.Canvas2.transform, false);

			_loginButton = _selfPage.transform.Find("Login_Group/Button_Login").GetComponent<ButtonShiftingContent>();
			_loginButton.onClick.AddListener(PressedLoginHandler);

			_betaButton = _selfPage.transform.Find("Beta_Group/Button_Beta").GetComponent<ButtonShiftingContent>();
			_betaButton.onClick.AddListener(PressedBetaHandler);

			_waitingButton = _selfPage.transform.Find("Waiting_Group/Button_Waiting").GetComponent<ButtonShiftingContent>();
			_waitingButton.onClick.AddListener(PressedWaitingHandler);
	        _waitingButton.gameObject.SetActive(false);

			_betaInput = _selfPage.transform.Find("Beta_Group/InputField_Beta").GetComponent<InputField>();

            Hide();
        }


		public void Dispose()
		{
		}

	    private async void PressedLoginHandler () 
	    {
		    Debug.Log("state = " + _state);
			GameClient.Get<ISoundManager>().PlaySound(Common.Enumerators.SoundType.CLICK, Constants.SFX_SOUND_VOLUME, false, false, true);
			if (_state == "login") {
				//Here we will begin the login procedure
				_waitingButton.transform.parent.gameObject.SetActive(true);
				_loginButton.transform.parent.gameObject.SetActive (false);
				_state = "waiting_from_login";
				//popup can only be Hide() once login is successful, and we go to the main menu page
				
				await LoomManager.Instance.SetUser();
				_dataManager.StartLoadBackend(SuccessfulLogin);
			}
		}

	    private void PressedWaitingHandler () {
			if (_state == "waiting_from_login") {
				//Interrupt the login process
				_waitingButton.transform.parent.gameObject.SetActive(false);
				_loginButton.transform.parent.gameObject.SetActive (true);
				_state = "login";
			} else if (_state == "waiting_from_beta") {
				//Interrupt the login process
				_waitingButton.transform.parent.gameObject.SetActive(false);
				_betaButton.transform.parent.gameObject.SetActive (true);
				_state = "beta";
			}
		}

	    private void PressedBetaHandler () {
			if (_state == "beta") {
				if (_betaInput.text.Length > 0) { //check if field is empty. Can replace with exact value once we know if there's a set length for beta keys
					//Here we will begin the beta key procedure
					_waitingButton.transform.parent.gameObject.SetActive (true);
					_betaButton.transform.parent.gameObject.SetActive (false);
					_state = "waiting_from_beta";
					//popup can only be Hide() once beta key is successful, and we go to the main menu page
				} else {
					_uiManager.DrawPopup<WarningPopup> ("Input a valid Beta Key");
				}
			}
		}

		private void SuccessfulLogin () {
			GameClient.Get<IAppStateManager>().ChangeAppState(Common.Enumerators.AppState.MAIN_MENU);
			Hide ();
		}

        public void Hide()
        {
            OnHidePopupEvent?.Invoke();
            _selfPage.SetActive(false);
		}

        public void SetMainPriority()
        {
        }

        public void Show()
        {
			_time = 0;
			_state = "beta";

			_betaButton.transform.parent.gameObject.SetActive (true);
			_waitingButton.transform.parent.gameObject.SetActive (false);
			_loginButton.transform.parent.gameObject.SetActive (false);

            _selfPage.SetActive(true);
        }

		public void Show(object data)
		{
			Show ();
		}

        public void Update()
        {
			//this is just for testing purposes of the popup, remove and let the login process handle hiding
			if (_state == "waiting_from_beta" )/*|| _state == "waiting_from_login" */ 
			{
				_time += Time.deltaTime;
				if (_time > 2) {
					_state = "login";
					_waitingButton.transform.parent.gameObject.SetActive(false);
					_loginButton.transform.parent.gameObject.SetActive (true);
					//SuccessfulLogin ();
				}
			}
        }

    }
}




