#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lutra.UI.Components;

namespace Lutra.Features.SafeZone.EditorTools
{
    /// <summary>
    /// Menú Lutra > Zona Segura > Montar habitación panorámica: convierte Panel_Room (escena abierta)
    /// en la habitación deslizable de <see cref="SafeZoneRoom"/>: ScrollRect horizontal, Viewport,
    /// RoomContent con los 3 trozos del fondo de día y los PlacementPoint dentro, para que se
    /// muevan con el dibujo. Conserva los índices de los puntos (están guardados en SQLite y Firestore).
    /// Se puede deshacer con Ctrl+Z. No crea ninguna escena: reorganiza Panel_Room dentro de Main.unity.
    /// Se puede volver a ejecutar sin riesgo: si la habitación ya está montada, solo reasigna los sprites
    /// y corrige la colocación de la barra de inventario (con su botón para plegarla) y de la cabecera.
    /// (Está dentro de LutraCore, por eso va entero en #if UNITY_EDITOR.)
    /// </summary>
    public static class SafeZoneRoomBuilder
    {
        private const string SpritesRoot = "Assets/Sprites/Refugio_seguro";

        private static readonly string[] DayPaths =
        {
            SpritesRoot + "/Dia/Fondo_refugio_dia_1.png",
            SpritesRoot + "/Dia/Fondo_refugio_dia_2.png",
            SpritesRoot + "/Dia/Fondo_refugio_dia_3.png",
        };

        private static readonly string[] NightPaths =
        {
            SpritesRoot + "/Noche/Fondo_refugio_noche_1.png",
            SpritesRoot + "/Noche/Fondo_refugio_noche_2.png",
            SpritesRoot + "/Noche/Fondo_refugio_noche_3.png",
        };

        [MenuItem("Lutra/Zona Segura/Montar habitación panorámica")]
        private static void Build()
        {
            var view = Object.FindAnyObjectByType<SafeZoneView>(FindObjectsInactive.Include);
            if (view == null)
            {
                EditorUtility.DisplayDialog("Zona Segura", "No hay ningún SafeZoneView en la escena abierta (abre Main.unity).", "OK");
                return;
            }

            var viewSo    = new SerializedObject(view);
            var roomPanel = viewSo.FindProperty("_roomPanel").objectReferenceValue as GameObject;
            if (roomPanel == null)
            {
                EditorUtility.DisplayDialog("Zona Segura", "SafeZoneView no tiene asignado _roomPanel.", "OK");
                return;
            }

            var day   = DayPaths.Select(_loadSprite).ToArray();
            var night = NightPaths.Select(_loadSprite).ToArray();
            if (day.Any(s => s == null) || night.Any(s => s == null))
            {
                EditorUtility.DisplayDialog("Zona Segura",
                    $"Faltan sprites del fondo en {SpritesRoot}/Dia y /Noche (Fondo_refugio_dia_1..3, Fondo_refugio_noche_1..3).", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Montar habitación panorámica");
            int undoGroup = Undo.GetCurrentGroup();

            var room = roomPanel.GetComponent<SafeZoneRoom>();
            if (room == null)
                room = _buildHierarchy(roomPanel, view, viewSo, day);

            // Sprites y referencias (también si ya estaba montada)
            var roomSo  = new SerializedObject(room);
            var content = roomSo.FindProperty("_content").objectReferenceValue as RectTransform;
            var panels  = content != null
                ? Enumerable.Range(1, 3).Select(i => content.Find($"Background_{i}")?.GetComponent<Image>()).ToArray()
                : new Image[3];

            _setArray(roomSo.FindProperty("_backgroundPanels"), panels);
            _setArray(roomSo.FindProperty("_daySprites"),       day);
            _setArray(roomSo.FindProperty("_nightSprites"),     night);
            roomSo.ApplyModifiedProperties();

            viewSo.Update();
            viewSo.FindProperty("_room").objectReferenceValue = room;
            viewSo.ApplyModifiedProperties();

            // El botón invisible del popup no puede quedarse activo: tapa toda la pantalla
            var backdrop = viewSo.FindProperty("_popupBackdrop").objectReferenceValue as Button;
            if (backdrop != null && backdrop.gameObject.activeSelf)
            {
                Undo.RecordObject(backdrop.gameObject, "Desactivar backdrop");
                backdrop.gameObject.SetActive(false);
            }

            _fixScreenLayout(viewSo, roomPanel);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(roomPanel.scene);
            Selection.activeGameObject = content != null ? content.gameObject : roomPanel;

            Debug.Log("[SafeZoneRoomBuilder] Habitación panorámica montada. Coloca los PlacementPoint dentro de RoomContent " +
                      "(coordenadas en píxeles del arte; el trozo central va de x=-540 a x=540).");
        }

        private static SafeZoneRoom _buildHierarchy(GameObject roomPanel, SafeZoneView view, SerializedObject viewSo, Sprite[] day)
        {
            var panelRt = (RectTransform)roomPanel.transform;
            Undo.RecordObject(panelRt, "Panel_Room");

            // Pantalla completa y por detrás de la cabecera
            panelRt.anchorMin        = Vector2.zero;
            panelRt.anchorMax        = Vector2.one;
            panelRt.pivot            = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta        = Vector2.zero;
            Undo.SetSiblingIndex(panelRt, 0, "Panel_Room");

            // Viewport
            var viewport = _createRect("Viewport", panelRt);
            viewport.SetSiblingIndex(0);   // el resto de hijos (InventoryBar) queda por encima
            _stretch(viewport);
            Undo.AddComponent<RectMask2D>(viewport.gameObject);

            // RoomContent: mide lo mismo que el arte completo
            Vector2 tile    = day[0].rect.size;
            var     content = _createRect("RoomContent", viewport);
            content.anchorMin        = new Vector2(0f, 0.5f);
            content.anchorMax        = new Vector2(0f, 0.5f);
            content.pivot            = new Vector2(0f, 0.5f);
            content.sizeDelta        = new Vector2(tile.x * day.Length, tile.y);
            content.anchoredPosition = Vector2.zero;

            for (int i = 0; i < day.Length; i++)
            {
                var bg = _createRect($"Background_{i + 1}", content);
                bg.anchorMin = new Vector2((float)i / day.Length, 0f);
                bg.anchorMax = new Vector2((float)(i + 1) / day.Length, 1f);
                bg.offsetMin = bg.offsetMax = Vector2.zero;

                var img = Undo.AddComponent<Image>(bg.gameObject);
                img.sprite        = day[i];
                img.raycastTarget = true;   // recibe el arrastre del dedo y se lo pasa al ScrollRect
            }

            // PlacementPoints: dentro del contenido, en el trozo central, con la misma posición que tenían
            var points = new List<PlacementPoint>();
            var arr    = viewSo.FindProperty("_placementPoints");
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue is PlacementPoint p) points.Add(p);
            foreach (var p in roomPanel.GetComponentsInChildren<PlacementPoint>(true))
                if (!points.Contains(p)) points.Add(p);

            foreach (var p in points)
            {
                var rt = (RectTransform)p.transform;
                Vector2 pos = rt.anchoredPosition;
                Undo.SetTransformParent(rt, content, "PlacementPoint");
                Undo.RecordObject(rt, "PlacementPoint");
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = pos;
                rt.localScale       = Vector3.one;
            }

            // ScrollRect horizontal
            var scroll = Undo.AddComponent<ScrollRect>(roomPanel);
            scroll.content           = content;
            scroll.viewport          = viewport;
            scroll.horizontal        = true;
            scroll.vertical          = false;
            scroll.movementType      = ScrollRect.MovementType.Clamped;
            scroll.inertia           = true;
            scroll.decelerationRate  = 0.135f;
            scroll.scrollSensitivity = 10f;

            var room = Undo.AddComponent<SafeZoneRoom>(roomPanel);
            var so   = new SerializedObject(room);
            so.FindProperty("_content").objectReferenceValue = content;
            so.ApplyModifiedProperties();

            // Centrar el trozo central en el editor
            float scale = viewport.rect.height > 0f ? viewport.rect.height / content.rect.height : 1f;
            content.localScale       = new Vector3(scale, scale, 1f);
            content.anchoredPosition = new Vector2(-(content.rect.width * scale - viewport.rect.width) * 0.5f, 0f);

            return room;
        }

        /// <summary>
        /// Panel_Room ocupa ahora toda la pantalla, así que la barra de inventario (anclada a su borde
        /// inferior) se recoloca justo encima de la BottomNavBar, y el popup del inventario encima de ella.
        /// La cabecera (Tienda / Habitación) se dibuja por encima de Panel_Shop para que sus botones
        /// se puedan pulsar con la tienda abierta.
        /// </summary>
        private static void _fixScreenLayout(SerializedObject viewSo, GameObject roomPanel)
        {
            const float margin = 20f;
            var screen = roomPanel.transform.parent;

            // Barra de inventario (a la derecha del botón que la pliega), justo encima de la BottomNavBar
            var bar = viewSo.FindProperty("_inventoryBar").objectReferenceValue as GameObject;
            float barTop = 0f;
            if (bar != null && bar.transform.parent == roomPanel.transform)
            {
                var nav       = Object.FindAnyObjectByType<BottomNavBar>(FindObjectsInactive.Include);
                float navTop  = nav != null ? ((RectTransform)nav.transform).rect.height : 0f;
                var barRt     = (RectTransform)bar.transform;
                float height  = barRt.rect.height > 0f ? barRt.rect.height : 160f;

                var toggle = _ensureInventoryToggle(viewSo, roomPanel, height);
                var toggleRt = (RectTransform)toggle.transform;
                Undo.RecordObject(toggleRt, "InventoryToggleButton");
                toggleRt.anchorMin = toggleRt.anchorMax = Vector2.zero;
                toggleRt.pivot            = Vector2.zero;
                toggleRt.sizeDelta        = new Vector2(height, height);
                toggleRt.anchoredPosition = new Vector2(margin, navTop + margin);

                Undo.RecordObject(barRt, "InventoryBar");
                barRt.anchorMin = new Vector2(0f, 0f);
                barRt.anchorMax = new Vector2(1f, 0f);
                barRt.pivot     = new Vector2(0.5f, 0f);
                barRt.offsetMin = new Vector2(margin + height + margin, navTop + margin);
                barRt.offsetMax = new Vector2(-margin, navTop + margin + height);
                Undo.SetSiblingIndex(barRt, roomPanel.transform.childCount - 1, "InventoryBar");
                Undo.SetSiblingIndex(toggleRt, roomPanel.transform.childCount - 1, "InventoryToggleButton");
                barTop = navTop + margin + height;
            }

            // Popup del inventario, justo encima de la barra
            var popup = viewSo.FindProperty("_inventoryItemPopup").objectReferenceValue as GameObject;
            if (popup != null && barTop > 0f && popup.transform.parent == screen)
            {
                var popupRt = (RectTransform)popup.transform;
                if (Mathf.Approximately(popupRt.anchorMin.y, 0f) && Mathf.Approximately(popupRt.anchorMax.y, 0f))
                {
                    Undo.RecordObject(popupRt, "InventoryItemPopUp");
                    popupRt.anchoredPosition = new Vector2(popupRt.anchoredPosition.x,
                        barTop + margin + popupRt.rect.height * popupRt.pivot.y);
                }
            }

            // Cabecera por encima de la tienda (los diálogos siguen por encima de la cabecera)
            var tab   = viewSo.FindProperty("_roomTabButton").objectReferenceValue as Button;
            var shop  = viewSo.FindProperty("_shopPanel").objectReferenceValue as GameObject;
            var header = tab != null ? _directChildOf(screen, tab.transform) : null;
            if (header != null && shop != null && shop.transform.parent == screen
                && header.GetSiblingIndex() < shop.transform.GetSiblingIndex())
            {
                Undo.SetSiblingIndex(header, shop.transform.GetSiblingIndex(), "Header");
            }
        }

        /// <summary>
        /// Crea (si no existe) el botón que abre y cierra la barra de inventario, con el mismo estilo
        /// que el botón de la pestaña Habitación, y lo asigna en SafeZoneView. El texto lo pone el código.
        /// </summary>
        private static Button _ensureInventoryToggle(SerializedObject viewSo, GameObject roomPanel, float size)
        {
            viewSo.Update();
            var existing = viewSo.FindProperty("_inventoryToggleButton").objectReferenceValue as Button;
            if (existing != null) return existing;

            var styleButton = viewSo.FindProperty("_roomTabButton").objectReferenceValue as Button;
            var styleImage  = styleButton != null ? styleButton.GetComponent<Image>() : null;
            var styleLabel  = styleButton != null ? styleButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;

            var rt = _createRect("InventoryToggleButton", roomPanel.transform);
            rt.sizeDelta = new Vector2(size, size);

            var img = Undo.AddComponent<Image>(rt.gameObject);
            if (styleImage != null)
            {
                img.sprite = styleImage.sprite;
                img.type   = styleImage.type;
                img.color  = styleImage.color;
            }

            var button = Undo.AddComponent<Button>(rt.gameObject);
            button.targetGraphic = img;

            var labelRt = _createRect("Label", rt);
            _stretch(labelRt);
            labelRt.offsetMin = new Vector2(8f, 8f);
            labelRt.offsetMax = new Vector2(-8f, -8f);
            var label = Undo.AddComponent<TextMeshProUGUI>(labelRt.gameObject);
            label.text             = "Objetos";
            label.alignment        = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin      = 18f;
            label.fontSizeMax      = 40f;
            label.raycastTarget    = false;
            if (styleLabel != null)
            {
                label.font               = styleLabel.font;
                label.fontSharedMaterial = styleLabel.fontSharedMaterial;
                label.color              = styleLabel.color;
            }

            viewSo.FindProperty("_inventoryToggleButton").objectReferenceValue = button;
            viewSo.FindProperty("_inventoryToggleLabel").objectReferenceValue  = label;
            viewSo.ApplyModifiedProperties();
            return button;
        }

        private static Transform _directChildOf(Transform parent, Transform t)
        {
            while (t != null && t.parent != parent) t = t.parent;
            return t;
        }

        private static RectTransform _createRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, name);
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void _stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Sprite _loadSprite(string path)
            => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

        private static void _setArray(SerializedProperty prop, Object[] values)
        {
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
#endif
