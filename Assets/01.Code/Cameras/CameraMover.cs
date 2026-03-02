using System;
using UnityEngine;

namespace _01.Code.Cameras
{
    public class CameraMover : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private float speed = 5f;

        [SerializeField] private Vector2 minPos = new Vector2(-20f, -20f);
        [SerializeField] private Vector2 maxPos = new Vector2(20f, 20f);

        [HideInInspector] public Vector2 direction;

        private void FixedUpdate()
        {
            Move();
            ClampPosition();
        }

        private void Move()
        {
            rb.linearVelocity = direction * speed;
        }

        private void ClampPosition()
        {
            Vector2 pos = rb.position;

            pos.x = Mathf.Clamp(pos.x, minPos.x, maxPos.x);
            pos.y = Mathf.Clamp(pos.y, minPos.y, maxPos.y);

            rb.position = pos;
        }
    }
}