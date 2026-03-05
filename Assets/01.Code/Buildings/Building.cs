using System;
using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class Building : PlaceableEntity
    {
        public event Action OnClick;
    }
}