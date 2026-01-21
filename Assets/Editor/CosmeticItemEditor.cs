#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CosmeticItem))]
public class CosmeticItemEditor : Editor
{
    SerializedProperty id;
    SerializedProperty displayName;
    SerializedProperty category;
    SerializedProperty unlockOnStageClear;
    SerializedProperty priceCoins;
    SerializedProperty icon;

    // Ship
    SerializedProperty shipSprite;

    // Weapon - Core
    SerializedProperty damageMul;
    SerializedProperty speedMul;
    SerializedProperty fireIntervalMul;
    SerializedProperty shotCount;
    SerializedProperty spreadAngle;

    // Weapon - Major
    SerializedProperty usePierce;
    SerializedProperty pierceCount;

    SerializedProperty useHoming;
    SerializedProperty homingStrength;
    SerializedProperty turnRate;

    SerializedProperty useExplosion;
    SerializedProperty explosionRadius;

    // Weapon - Minor
    SerializedProperty hitRadiusMul;
    SerializedProperty critChance;
    SerializedProperty critMul;
    SerializedProperty slowPercent;
    SerializedProperty slowTime;

    // Internal
    SerializedProperty maxWeight;

    private void OnEnable()
    {
        id = serializedObject.FindProperty("id");
        displayName = serializedObject.FindProperty("displayName");
        category = serializedObject.FindProperty("category");
        unlockOnStageClear = serializedObject.FindProperty("unlockOnStageClear");
        priceCoins = serializedObject.FindProperty("priceCoins");
        icon = serializedObject.FindProperty("icon");

        shipSprite = serializedObject.FindProperty("shipSprite");

        damageMul = serializedObject.FindProperty("damageMul");
        speedMul = serializedObject.FindProperty("speedMul");
        fireIntervalMul = serializedObject.FindProperty("fireIntervalMul");
        shotCount = serializedObject.FindProperty("shotCount");
        spreadAngle = serializedObject.FindProperty("spreadAngle");

        usePierce = serializedObject.FindProperty("usePierce");
        pierceCount = serializedObject.FindProperty("pierceCount");

        useHoming = serializedObject.FindProperty("useHoming");
        homingStrength = serializedObject.FindProperty("homingStrength");
        turnRate = serializedObject.FindProperty("turnRate");

        useExplosion = serializedObject.FindProperty("useExplosion");
        explosionRadius = serializedObject.FindProperty("explosionRadius");

        hitRadiusMul = serializedObject.FindProperty("hitRadiusMul");
        critChance = serializedObject.FindProperty("critChance");
        critMul = serializedObject.FindProperty("critMul");
        slowPercent = serializedObject.FindProperty("slowPercent");
        slowTime = serializedObject.FindProperty("slowTime");

        maxWeight = serializedObject.FindProperty("maxWeight");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawCommon();
        EditorGUILayout.Space(10);

        CosmeticCategory cat = (CosmeticCategory)category.enumValueIndex;

        switch (cat)
        {
            case CosmeticCategory.ShipSkin:
                DrawShip();
                break;

            case CosmeticCategory.Weapon:
                DrawWeapon();
                break;

            default:
                EditorGUILayout.HelpBox("현재 이 카테고리는 Inspector 전용 UI가 아직 없습니다.", MessageType.Info);
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCommon()
    {
        EditorGUILayout.LabelField("COMMON", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(id);
        EditorGUILayout.PropertyField(displayName);
        EditorGUILayout.PropertyField(category);
        EditorGUILayout.PropertyField(unlockOnStageClear);
        EditorGUILayout.PropertyField(priceCoins);
        EditorGUILayout.PropertyField(icon);
    }

    private void DrawShip()
    {
        EditorGUILayout.LabelField("SHIP SKIN", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(shipSprite);
    }

    private void DrawWeapon()
    {
        EditorGUILayout.LabelField("WEAPON - CORE", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(damageMul);
        EditorGUILayout.PropertyField(speedMul);
        EditorGUILayout.PropertyField(fireIntervalMul);
        EditorGUILayout.PropertyField(shotCount);
        EditorGUILayout.PropertyField(spreadAngle);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("WEAPON - MAJOR (1 only)", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(usePierce);
        if (usePierce.boolValue)
            EditorGUILayout.PropertyField(pierceCount);

        EditorGUILayout.PropertyField(useHoming);
        if (useHoming.boolValue)
        {
            EditorGUILayout.PropertyField(homingStrength);
            EditorGUILayout.PropertyField(turnRate);
        }

        EditorGUILayout.PropertyField(useExplosion);
        if (useExplosion.boolValue)
            EditorGUILayout.PropertyField(explosionRadius);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("WEAPON - MINOR", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(hitRadiusMul);
        EditorGUILayout.PropertyField(critChance);
        EditorGUILayout.PropertyField(critMul);
        EditorGUILayout.PropertyField(slowPercent);
        EditorGUILayout.PropertyField(slowTime);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("WEAPON - INTERNAL", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(maxWeight);
    }
}
#endif
