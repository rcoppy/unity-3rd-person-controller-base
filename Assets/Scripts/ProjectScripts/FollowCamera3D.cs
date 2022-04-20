using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Net.Sockets;

[RequireComponent(typeof(SphereCoords))]
public class FollowCamera3D : MonoBehaviour
{

    [SerializeField]
    Transform _target; // used for cartesian tracking

    [SerializeField]
    bool _trackRotationToTarget = true; 

    [SerializeField]
    float _lookAheadFactor = 0.67f; // multiplier of sphere coords radius

    [SerializeField] private float _lookAheadSeconds = 1.5f; 
    
    [SerializeField]
    [Tooltip("Gets multiplied by deltaTime")]
    float _lookLerp = 1f; 

    Vector3 _lookTarget; // used for angular tracking

    [SerializeField]
    [Tooltip("Gets multiplied by deltaTime")]
    float _angularLerpFactor = 1f; // degrees

    [SerializeField] private float _minDistanceToTarget = 1f;
    
    [SerializeField]
    Vector3 _safeZoneExtent;

    [SerializeField]
    Vector3 _safezoneOffset;


    [SerializeField]
    Vector3 _outerZoneExtent;

    [SerializeField]
    Vector3 _outerZoneOffset;


    bool _moving = false;

    Vector3 _velocity;
    // Quaternion _angularVelocity; 

    [SerializeField]
    float _friction = 0.1f;

    [SerializeField]
    float _acceleration = 12f;

    [SerializeField]
    float _catchupDampingFactor = 3.5f; 

    [SerializeField]
    float _maxSpeed = 20f;

    [SerializeField]
    bool _killAcceleration = true;

    [SerializeField]
    bool _scaleBoundsWithAspect = true;

    float _aspectScaling = 1f;

    float _baseAspectScaling = 1f;

    float _baseAspectRatio = 16f / 9f;

    Vector3 _originPosition; 

    //[SerializeField]
    //AdaptiveAspectRatio _aspectTracker;

    //AdaptCameraSizeToAspect _aspectAdapter;

    Vector3 _lastTargetPosition;

    [SerializeField]
    Vector3 _cameraForward = Vector3.zero; 

    SphereCoords _sphereCoords;

    // moving average velocity
    Vector3[] _velocityFrames;
    int _velFrameId;
    Vector3 _averageVelocity; 

    private void HandleAspectUpdate(float ratio)
    {
        _aspectScaling = _baseAspectScaling * ratio / _baseAspectRatio; 
    }

    private void OnEnable()
    {
        //if (_aspectTracker)
        //{
        //    _aspectTracker.OnAspectUpdated += HandleAspectUpdate;
        //}
    }

    private void OnDisable()
    {
        //if (_aspectTracker)
        //{
        //    _aspectTracker.OnAspectUpdated -= HandleAspectUpdate;
        //}
    }

    private void Awake()
    {
        _velocity = Vector3.zero;
        _velocityFrames = new Vector3[30];
        _velFrameId = 0;

        for (int i = 0; i < _velocityFrames.Length; i++)
        {
            _velocityFrames[i] = Vector3.zero; 
        }

        _lookTarget = _target.position; 
        _lastTargetPosition = _target.position;
        _originPosition = transform.position; 

        //if (_aspectTracker)
        //{
        //    _baseAspectRatio = _aspectTracker.AspectRatio;
        //}

        //_aspectAdapter = GetComponent<AdaptCameraSizeToAspect>();

        _sphereCoords = GetComponent<SphereCoords>();
    }

    void UpdateAverageVelocity()
    {
        _velFrameId %= _velocityFrames.Length;

        _velocityFrames[_velFrameId] = GetInstantaneousTargetVelocity();

        Vector3 total = Vector3.zero;

        for (int i = 0; i < _velocityFrames.Length; i++)
        {
            total += _velocityFrames[i];
        }

        _averageVelocity = total / _velocityFrames.Length;
        _velFrameId++; 
    }

    void UpdateYaw()
    {
        // TODO: work in progress
        if ((_lookTarget - _target.position).magnitude > 1f)
        {
            Vector3 toTarget = _lookTarget - _target.position;
            Vector3 toCamera = transform.position - _target.position;

            float angleDifference = Vector3.Angle(toTarget, toCamera);
            Vector3 linearDifference = toCamera - toTarget;

            float sign = -1f * Mathf.Sign(Vector3.Dot(transform.right, linearDifference));

            if (angleDifference > 10f)
            {
                _sphereCoords.yaw -= sign * angleDifference * Time.deltaTime;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        UpdateAverageVelocity();

        float scaleFactor = GetScalingFromCamera();

        var outerBounds = new Bounds(_originPosition + scaleFactor * _outerZoneOffset, scaleFactor * _outerZoneExtent);
        var innerBounds = new Bounds(_originPosition + scaleFactor * _safezoneOffset, scaleFactor * _safeZoneExtent);

        var playerPos = _target.transform.position + _safezoneOffset;

        bool isRetreating = false; 

        if (!_moving && !outerBounds.Contains(playerPos))
        {
            _moving = true;

            // reset velocity
            if (_killAcceleration)
            {
                _velocity = Vector3.zero;
            }

            //Vector3 targetPos = _target.position;
            //targetPos.z = transform.position.z;

            //var direction = (targetPos - transform.position).normalized;
            //_velocity = direction * _maxSpeed; 

        }
        else if (innerBounds.SqrDistance(playerPos) < _minDistanceToTarget * _minDistanceToTarget)
        {
            if ((_target.position - transform.position).magnitude < _minDistanceToTarget * 0.90f)
            {
                _moving = true;
                isRetreating = true; 
            }
            else
            {
                _moving = false; 
            }
        }

        if (_originPosition.y < playerPos.y)
        {
            _moving = true; 
            _velocity += Vector3.up * 2.5f * Time.deltaTime; 
        }

        if (_moving && !isRetreating)
        {
            // Vector3 targetVelocity = _averageVelocity;
            // float damping = 1f;
            
            // if camera is moving opposite the player
            // slow it down
            if (Vector3.Dot(_averageVelocity, _velocity) < 0f)
            {
                _velocity *= 0.35f;
            }
            
            // Vector3 targetPos = playerPos; // _target.position;
            // // targetPos.z = transform.position.z;
            //
            // var direction = (targetPos - _originPosition).normalized;
            // var acceleration = direction * _acceleration;
            //
            // _velocity *= damping;
            //
            // _velocity += Time.deltaTime * acceleration;

            

            // if (_averageVelocity.sqrMagnitude < 0.3f && !outerBounds.Contains(playerPos))
            // {
            //     var direction = (playerPos - _originPosition).normalized;
            //     var acceleration = direction * _acceleration;
            //
            //     _velocity = Vector3.Lerp(_velocity, direction * _maxSpeed, Time.deltaTime * _lookLerp);
            // }
            // else
            // {
            //     _velocity = Vector3.Lerp(_velocity, _averageVelocity, Time.deltaTime * _lookLerp);
            // }
            
            var trackDirection = (playerPos + _averageVelocity * _lookAheadSeconds - _originPosition).normalized;

            var trackSpeed = Mathf.Min(_maxSpeed,
                Vector3.Dot(trackDirection, _velocity) + _acceleration * Time.deltaTime);

            var compensation = Mathf.Sign(trackSpeed) * trackSpeed * Mathf.Abs(Vector3.Dot(_averageVelocity.normalized, trackDirection)); 

            var trackComponent = Mathf.Max(0f, trackSpeed - compensation) * trackDirection;


            // var lookAheadDirection = _averageVelocity.normalized; 
            // var lookAheadSpeed = Vector3.Dot(lookAheadDirection, _velocity);
            //
            // var lookAheadComponent = lookAheadDirection * lookAheadSpeed; 
            
            _velocity = Vector3.Lerp(_velocity, trackComponent + _averageVelocity, Time.deltaTime * _lookLerp);
            
            
            // spherecoords pitch adjustment
            var alignment = Vector3.Dot(_averageVelocity.normalized, transform.forward);
            var cutoff = Mathf.Cos(Mathf.Deg2Rad * 45f);

            if (Mathf.Abs(alignment) > cutoff)
            {
                float maxPitch, minPitch;
                maxPitch = 40f;
                minPitch = 7f;

                _sphereCoords.pitch = Mathf.LerpAngle(_sphereCoords.pitch,
                    Mathf.Sign(alignment) < 0 ? maxPitch : minPitch, Time.deltaTime);
            } 
            else
            {
                _sphereCoords.pitch = Mathf.LerpAngle(_sphereCoords.pitch, 28f,Time.deltaTime);
                
                // handle yawing 
                _sphereCoords.yaw += 3f * Mathf.Sign(alignment) * Time.deltaTime; 
            }

        } else if (_moving && isRetreating)
        {
            _velocity += (transform.position - _target.position).normalized * _acceleration * Time.deltaTime;
        }
        else
        {
            // if velocity is large and inner bounds are tiny
            // camera can yo-yo dizzyingly

            // also experimented with scaling acceleration / max velocity
            // by reciprocal of scale factor but results were too snappy

            if (_velocity.magnitude > innerBounds.extents.magnitude)
            {
                _velocity *= 0.3f;
            }
            else
            {
                _velocity *= (1f - _friction);
            }
        }

        // _velocity = Vector3.ClampMagnitude(_velocity, _maxSpeed);

        _originPosition += _velocity * Time.deltaTime;

        _lastTargetPosition = _target.position;

        transform.position = _originPosition + _sphereCoords.GetRectFromSphere();

        if (_trackRotationToTarget)
        {
            // camera rotation

            var constructedVelocity = _averageVelocity;
            constructedVelocity.y = 0; 
            
            Vector3 lookPos = _target.position + _lookAheadSeconds * constructedVelocity;

            // var vel = _averageVelocity;
            // var axis = transform.right;

            // var dot = Vector3.Dot(axis, vel);

            // if (Mathf.Abs(dot) > 0.6f)
            //{
            //    // todo: editor-expose the lookahead distance? 
            //    float dist = _lookAheadFactor * _sphereCoords.radius;

            //    // character move direction
            //    float sign = dot < 0f ? -1f : 1f;

            //    lookPos += sign * dist * axis; 
            //}

            // lookPos += vel; 

            _lookTarget = lookPos; // Vector3.Lerp(_lookTarget, lookPos, _lookAheadFactor);

            Quaternion temp = transform.rotation;
            transform.LookAt(_lookTarget, Vector3.up);
            Quaternion targetRotation = transform.rotation;

            transform.rotation = Quaternion.Slerp(temp, targetRotation, _angularLerpFactor * Time.deltaTime);
            
            // UpdateYaw();
            

        }

        if (_averageVelocity.magnitude > 0.8f && Vector3.Dot(transform.forward, _averageVelocity) < -0.7f)
        {
            float angleDifference = Vector3.Angle(_averageVelocity, transform.forward) % 180f;
            Vector3 linearDifference = _averageVelocity - transform.forward;

            float sign = Mathf.Sign(Vector3.Dot(transform.right, linearDifference));

            // if (angleDifference > 90f)
            // {
            //     _sphereCoords.yaw += sign * angleDifference * Time.deltaTime;
            // }

            //if ((_target.position - transform.position).magnitude < 2f)
            //{
                //_sphereCoords.yaw += 100f * Time.deltaTime;
            //}
        }
    }

    Vector3 GetInstantaneousTargetVelocity()
    {
        return (_target.position - _lastTargetPosition) / Time.deltaTime; 
    }

    float GetScalingFromCamera()
    {
        float scaleFactor = 1f;

        // scaling based on aspect ratio
        //if (_scaleBoundsWithAspect && _aspectTracker)
        //{
        //    scaleFactor = _aspectScaling;

        //    if (_aspectAdapter)
        //    {
        //        scaleFactor /= _aspectAdapter.PortraitModeScaleFactor;
        //    }
        //}

        return scaleFactor; 
    }

    // editor visualization
    void OnDrawGizmos()
    { 
        Color lineColor = Color.yellow;

        float scaleFactor = GetScalingFromCamera();

        Vector3 pos = transform.position;

        if (Application.isPlaying)
        {
            pos = _originPosition; 
        }

        Gizmos.color = lineColor;
        Gizmos.DrawWireCube(pos + scaleFactor * _safezoneOffset, scaleFactor * _safeZoneExtent);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(pos + scaleFactor * _outerZoneOffset, scaleFactor * _outerZoneExtent);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_target.position, _minDistanceToTarget);

    }
}
