using System;
using System.Collections.Generic;
using Yoegoe.Core;
using Yoegoe.Data;
using UnityEngine;

namespace Yoegoe.Cooking
{
    /// <summary>재료 13종 (채집6·사냥6·물). Docs/00 부록 A.</summary>
    public enum CookingIngredientId
    {
        Water = 0,   // 물
        Chili,       // 고추
        Rice,        // 쌀
        RedBean,     // 팥
        Fruit,       // 과실
        Namul,       // 산나물
        Herb,        // 약재
        Honey,       // 꿀
        Boar,        // 멧돼지고기
        Bird,        // 새고기
        Fish,        // 물고기
        Egg,         // 새알
        Oil,         // 기름
        Count
    }

    public enum CookingCharmType
    {
        None = 0,
        PlusFive,    // +5초
        Diagonal,    // 대각선
        Clairvoyance,// 천리안
        Recycle,     // 회수
        Double,      // 몰빵
        Cancel       // 나가리 (게임 중)
    }

    public enum CookingResultKind
    {
        Food,
        Offering
    }

    public readonly struct CookingRecipe
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly CookingResultKind Kind;
        public readonly CookingIngredientId[] Ingredients; // sorted multiset

        public CookingRecipe(string id, string name, CookingResultKind kind, params CookingIngredientId[] ingredients)
        {
            Id = id;
            DisplayName = name;
            Kind = kind;
            Ingredients = ingredients ?? Array.Empty<CookingIngredientId>();
            Array.Sort(Ingredients);
        }
    }
}
