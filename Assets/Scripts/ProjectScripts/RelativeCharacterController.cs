using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

namespace GGJ2022
{
    public class RelativeCharacterController : MonoBehaviour
    {
        public static Vector3 WorldUp = Vector3.up;

        bool _isInputEnabled = true;

        bool _isLateralInputActive = false;

        Vector2 _moveInputVector;

        [SerializeField]
        float _forwardAcceleration = 10f;

        [SerializeField]
        public float _maxForwardSpeed = 3.5f;

        [SerializeField]
        public float _jumpHeight = 6f;

        [SerializeField]
        float _groundFriction = 0.025f;

        Rigidbody _rigidbody;

        // [SerializeField]
        Collider _boundingCapsule;

        [SerializeField]
        Collider _groundCheckSphere;

        [SerializeField]
        Camera _referenceCamera;

        [SerializeField]
        LayerMask _groundLayerMask; 

        // there is a child nested in this gameobject holding the actual visual character representation
        // i.e. 'the puppet'
        Animator _puppetAnimator; 
        GameObject _puppet;

        private Vector2 _lastNonzeroMoveVector;

        public bool IsOnGround
        {
            get { return _groundCollider != null; }
        }

        Collider _groundCollider = null; 

        public Camera ReferenceCamera
        {
            set { _referenceCamera = value; }
        }

        public void SetIsInputEnabled(bool flag)
        {
            _isInputEnabled = flag; 
        }

        // Added a toggle version for single pause/unpause event call
        public void ToggleIsInputEnabled()
        {
            if(!_isInputEnabled)
            {
                _isInputEnabled = true;
            }
            else
            {
                _isInputEnabled = false;
            }
        }


        [SerializeField]
        public UnityEvent OnWalkStarted;

        [SerializeField]
        public UnityEvent OnWalkEnded;

        [SerializeField]
        public UnityEvent OnJumpStarted;

        [SerializeField]
        public UnityEvent OnJumpEnded;

        bool _isWalking = false; 

        // Start is called before the first frame update
        void Start()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _boundingCapsule = GetComponent<Collider>();
        
            _puppet = transform.GetChild(0).gameObject;
            // _puppetAnimator = _puppet.GetComponent<Animator>();
            //
            // if (_puppetAnimator == null)
            // {
            //     // animator could be deeply nested 
            //     _puppetAnimator = _puppet.GetComponentInChildren<Animator>();
            // }
        }


        void UpdatePuppet()
        {
            var vel = _rigidbody.velocity;
            vel.y = 0;

            if (vel.magnitude > 0.1f)
            {
                var target = Quaternion.LookRotation(vel, transform.up);
                _puppet.transform.rotation = Quaternion.Slerp(_puppet.transform.rotation, target, 0.08f);
            }
        }
        
        // Update is called once per frame
        void FixedUpdate()
        {
            UpdateIsGrounded();

            Vector3 velUp = _rigidbody.velocity.y * Vector3.up;
            Vector3 velLateral = _rigidbody.velocity - velUp;

            if (_isInputEnabled)
            {
                var direction = GetMoveDirectionFromInputVector();

                if (!_isWalking && IsOnGround && direction.magnitude > 0.085f)
                {
                    _isWalking = true;
                    OnWalkStarted?.Invoke();
                }

                if (velLateral.magnitude < _maxForwardSpeed)
                {
                    _rigidbody.AddForce(_forwardAcceleration * direction * _rigidbody.mass);
                }
            }


            // ground friction
            if (IsOnGround && !_isLateralInputActive)
            {
                // TODO: change to force-based
                velLateral *= Mathf.Max(0.1f, 1f - _groundFriction);

                _rigidbody.velocity = velLateral + velUp;
            }

            // clamp
            // if (velLateral.magnitude > _maxForwardSpeed)
            // {
            //     _rigidbody.velocity = _maxForwardSpeed * velLateral.normalized + velUp; 
            // }



            //float lateralSpeed = Mathf.Min(velLateral.magnitude, _maxForwardSpeed);

            velUp = _rigidbody.velocity.y * Vector3.up;
            velLateral = _rigidbody.velocity - velUp;

            if (velLateral.magnitude < 0.08f && _isWalking)
            {
                OnWalkEnded?.Invoke();
                _isWalking = false; 
            }
            
            UpdatePuppet();
        }

        public void OnJumpInputReceived(InputAction.CallbackContext context)
        {
            // Debug.Log("Jump input");

            // https://forum.unity.com/threads/player-input-component-triggering-events-multiple-times.851959/
            if (context.performed)
            {
                string outcome = TryTriggerJump() ? "succeeded" : "failed";

                Debug.Log($"Jump {outcome}");
            }
        }

        // jump if on ground
        bool TryTriggerJump()
        {
            if (_isInputEnabled && IsOnGround)
            {
                _rigidbody.AddForce(CalculateJumpForce() * Vector3.up);

                if (_isWalking)
                {
                    _isWalking = false;
                    OnWalkEnded?.Invoke();
                }

                OnJumpStarted?.Invoke();

                return true;
            }
            return false; 
        }

        Vector3 GetMoveDirectionFromInputVector()
        {
            Vector3 up = WorldUp;
            Vector3 right = _referenceCamera.transform.right;
            Vector3 referenceForward = Vector3.forward; 

            Vector3 forward = Vector3.Cross(right, up).normalized;

            Quaternion rotation = Quaternion.FromToRotation(referenceForward, forward);

            if (IsOnGround)
            {
                // TODO: take slope of ground's surface normal at point of contact into account
            }

            Vector3 coord = new Vector3(_moveInputVector.x, 0f, _moveInputVector.y);

            // Debug.Log(rotation * coord);

            return rotation * coord; 
        }

        bool UpdateIsGrounded()
        {
            Collider[] hits;

            hits = Physics.OverlapSphere(_groundCheckSphere.bounds.center, _groundCheckSphere.bounds.extents.magnitude, _groundLayerMask);

            if (hits.Length < 1)
            {
                _groundCollider = null; 
                return false; 
            }

            if (_groundCollider == null)
            {
                OnJumpEnded?.Invoke();
            }

            _groundCollider = hits[0];

            return true;
        }

        float CalculateJumpForce()
        {
            /*
                F = (mass (targetVelocity - current_velocity)) / Time.deltaTime
             */

            // doesnt' work perfectly but if you play with the jump inputs 
            // you can get good results

            float h = _jumpHeight;
            float g = Physics.gravity.magnitude;

            float t_flight = Mathf.Sqrt(2 * h / g);

            float vf = h / t_flight + 0.5f * Physics.gravity.magnitude * t_flight;

            float m = _rigidbody.mass;
            float v0 = _rigidbody.velocity.y;
            float t_impulse = Time.fixedDeltaTime;

            return m * (vf - v0) / t_impulse;
        }

        public Vector3 GetIntendedSpatialDirection()
        {
            var forward = _referenceCamera.transform.forward;
            forward.y = 0; 
            
            var right = _referenceCamera.transform.right;
            right.y = 0;

            return (_lastNonzeroMoveVector.x * right + _lastNonzeroMoveVector.y * forward).normalized;
        }

        // map this to controls in player input component
        public void OnMoveInputReceived(InputAction.CallbackContext context)
        {
            if (context.canceled)
            {
                _moveInputVector = Vector2.zero;
                _isLateralInputActive = false;
            } else
            {
                _moveInputVector = context.ReadValue<Vector2>();
                _isLateralInputActive = true;
                _lastNonzeroMoveVector = _moveInputVector; 
            }
            
        }
    }
}
