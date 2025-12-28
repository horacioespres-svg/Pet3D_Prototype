using UnityEngine;

public class SistemaConstruccion : MonoBehaviour
{
    [Header("Configuraci�n")]
    public float tama�oGrid = 2.5f;
    public float distanciaSnap = 1.5f; // Distancia para activar snap magnético
    public float anchoPared = 2.0f; // Ancho de la pared para cálculo de offset

    [Header("Prefabs de Construcci�n")]
    public GameObject prefabParedActual;
    public GameObject prefabSueloActual;
    public GameObject prefabPuertaActual;
    public GameObject prefabVentanaActual;

    [Header("Materiales Preview")]
    public Material materialVerde;
    public Material materialRojo;

    private GameObject previewActual;
    private bool puedeColocar = false;
    private bool modoConstruccion = false;
    private float rotacionY = 0f;
    private float rotacionX = 0f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            modoConstruccion = !modoConstruccion;

            if (!modoConstruccion && previewActual != null)
            {
                Destroy(previewActual);
            }

            Debug.Log("Modo construcci�n: " + (modoConstruccion ? "ACTIVADO" : "DESACTIVADO"));
        }

        if (modoConstruccion)
        {
            GestionarRotacion();
            ActualizarPreview();

            if (Input.GetMouseButtonDown(0) && puedeColocar)
            {
                ColocarPieza();
            }

            if (Input.GetMouseButtonDown(1))
            {
                EliminarPieza();
            }
        }
    }

    void GestionarRotacion()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0f)
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                rotacionX += scroll > 0 ? 90f : -90f;
                rotacionX = Mathf.Round(rotacionX / 90f) * 90f;
            }
            else
            {
                rotacionY += scroll > 0 ? 90f : -90f;
                rotacionY = Mathf.Round(rotacionY / 90f) * 90f;
            }
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            rotacionY += 90f;
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            rotacionX += 90f;
        }

        if (previewActual != null)
        {
            previewActual.transform.rotation = Quaternion.Euler(rotacionX, rotacionY, 0f);
        }
    }

    Vector3 AplicarSnapMagnetico(Vector3 posicionBase, RaycastHit hit)
    {
        Vector3 posicionFinal = posicionBase;

        // Buscar TODAS las paredes en un área amplia
        Collider[] todasLasParedes = Physics.OverlapSphere(posicionBase, distanciaSnap * 3f);

        float distanciaMasCercana = float.MaxValue;
        Vector3 mejorPosicionSnap = posicionBase;
        string tipoSnap = "";

        Quaternion rotacionActual = Quaternion.Euler(rotacionX, rotacionY, 0f);
        Vector3 forwardParedNueva = rotacionActual * Vector3.forward;
        Vector3 rightParedNueva = rotacionActual * Vector3.right;

        foreach (Collider col in todasLasParedes)
        {
            if (!col.CompareTag("Construido")) continue;
            if (col.gameObject == previewActual) continue;

            Transform paredExistente = col.transform;
            Vector3 forwardParedExistente = paredExistente.forward;
            Vector3 rightParedExistente = paredExistente.right;
            Bounds bounds = col.bounds;
            Vector3 centroPared = bounds.center;

            // Calcular producto punto para detectar orientación
            float productoPunto = Mathf.Abs(Vector3.Dot(forwardParedExistente.normalized, forwardParedNueva.normalized));

            // ========== CASO 1: Paredes PERPENDICULARES (90 grados) ==========
            if (productoPunto < 0.3f)
            {
                // Calcular los bordes laterales de la pared existente
                Vector3 borde1 = centroPared + rightParedExistente * (bounds.size.x * 0.5f);
                Vector3 borde2 = centroPared - rightParedExistente * (bounds.size.x * 0.5f);

                // Offset de la nueva pared
                Vector3 offsetNuevaPared = rightParedNueva * (anchoPared * 0.5f);

                // Snap points para ambos bordes
                Vector3 snapPoint1 = new Vector3(borde1.x - offsetNuevaPared.x, posicionBase.y, borde1.z - offsetNuevaPared.z);
                Vector3 snapPoint2 = new Vector3(borde2.x - offsetNuevaPared.x, posicionBase.y, borde2.z - offsetNuevaPared.z);

                EvaluarSnapPoint(snapPoint1, posicionBase, ref distanciaMasCercana, ref mejorPosicionSnap, ref tipoSnap, "Perpendicular");
                EvaluarSnapPoint(snapPoint2, posicionBase, ref distanciaMasCercana, ref mejorPosicionSnap, ref tipoSnap, "Perpendicular");
            }
            // ========== CASO 2: Paredes PARALELAS (misma dirección) ==========
            else if (productoPunto > 0.7f)
            {
                // Snap lado a lado (izquierda y derecha)
                Vector3 ladoIzq = centroPared - rightParedExistente * (bounds.size.x * 0.5f);
                Vector3 ladoDer = centroPared + rightParedExistente * (bounds.size.x * 0.5f);

                Vector3 offsetNuevaPared = rightParedNueva * (anchoPared * 0.5f);

                Vector3 snapIzq = new Vector3(ladoIzq.x - offsetNuevaPared.x, posicionBase.y, ladoIzq.z - offsetNuevaPared.z);
                Vector3 snapDer = new Vector3(ladoDer.x + offsetNuevaPared.x, posicionBase.y, ladoDer.z + offsetNuevaPared.z);

                EvaluarSnapPoint(snapIzq, posicionBase, ref distanciaMasCercana, ref mejorPosicionSnap, ref tipoSnap, "Paralela");
                EvaluarSnapPoint(snapDer, posicionBase, ref distanciaMasCercana, ref mejorPosicionSnap, ref tipoSnap, "Paralela");
            }

            // ========== CASO 3: SNAP VERTICAL (apilar) ==========
            // Verificar si el cursor está cerca horizontalmente de la pared
            float distHorizontal = Vector3.Distance(
                new Vector3(posicionBase.x, 0, posicionBase.z),
                new Vector3(centroPared.x, 0, centroPared.z)
            );

            if (distHorizontal < anchoPared * 1.5f)
            {
                // Snap a la parte superior de la pared existente
                Vector3 snapArriba = new Vector3(centroPared.x, bounds.max.y, centroPared.z);

                float distVertical = Vector3.Distance(posicionBase, snapArriba);
                if (distVertical < distanciaMasCercana)
                {
                    distanciaMasCercana = distVertical;
                    mejorPosicionSnap = snapArriba;
                    tipoSnap = "Vertical (apilado)";
                }
            }
        }

        // Si encontramos un snap point cercano, ATRAER hacia él
        if (distanciaMasCercana < distanciaSnap)
        {
            posicionFinal = mejorPosicionSnap;
            Debug.Log($"SNAP {tipoSnap} activado! Distancia: {distanciaMasCercana:F2}m");
        }

        return posicionFinal;
    }

    void EvaluarSnapPoint(Vector3 snapPoint, Vector3 posicionCursor, ref float distanciaMasCercana, ref Vector3 mejorSnap, ref string tipoSnap, string tipo)
    {
        float distancia = Vector3.Distance(
            new Vector3(posicionCursor.x, 0, posicionCursor.z),
            new Vector3(snapPoint.x, 0, snapPoint.z)
        );

        if (distancia < distanciaMasCercana)
        {
            distanciaMasCercana = distancia;
            mejorSnap = snapPoint;
            tipoSnap = tipo;
        }
    }

    void ActualizarPreview()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            Vector3 posicionSnap = hit.point;

            if (hit.collider.CompareTag("Construido"))
            {
                float alturaBase = hit.collider.bounds.max.y;
                posicionSnap.y = alturaBase;
            }

            if (previewActual == null && prefabParedActual != null)
            {
                previewActual = Instantiate(prefabParedActual, posicionSnap, Quaternion.Euler(rotacionX, rotacionY, 0f));
                HacerTransparente(previewActual);
                DesactivarColliders(previewActual);
            }

            if (previewActual != null)
            {
                // Aplicar snap magnético a bordes de paredes perpendiculares
                posicionSnap = AplicarSnapMagnetico(posicionSnap, hit);

                previewActual.transform.position = posicionSnap;

                puedeColocar = !HayColision();

                CambiarColorPreview(puedeColocar ? materialVerde : materialRojo);
            }
        }
    }

    bool HayColision()
    {
        if (previewActual == null) return false;

        Renderer[] renderers = previewActual.GetComponentsInChildren<Renderer>();

        foreach (Renderer rend in renderers)
        {
            Bounds bounds = rend.bounds;

            // Detecci�n M�S ESTRICTA - solo 5% de tolerancia
            Collider[] colisiones = Physics.OverlapBox(bounds.center, bounds.extents * 0.95f, previewActual.transform.rotation);

            foreach (Collider col in colisiones)
            {
                if (col.gameObject != previewActual &&
                    !col.transform.IsChildOf(previewActual.transform) &&
                    !col.CompareTag("Player") &&
                    !col.GetComponent<Terrain>() &&
                    col.CompareTag("Construido"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    void EliminarPieza()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            if (hit.collider.CompareTag("Construido"))
            {
                Destroy(hit.collider.gameObject);
                Debug.Log("Pieza eliminada");
            }
        }
    }

    void ColocarPieza()
    {
        if (previewActual != null)
        {
            GameObject piezaReal = Instantiate(prefabParedActual, previewActual.transform.position, previewActual.transform.rotation);
            piezaReal.tag = "Construido";

            Debug.Log("Pieza colocada en: " + piezaReal.transform.position);
        }
    }

    void HacerTransparente(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            foreach (Material mat in rend.materials)
            {
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;

                Color color = mat.color;
                color.a = 0.5f;
                mat.color = color;
            }
        }
    }

    void DesactivarColliders(GameObject obj)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }

    void CambiarColorPreview(Material nuevoMaterial)
    {
        if (previewActual != null && nuevoMaterial != null)
        {
            Renderer[] renderers = previewActual.GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in renderers)
            {
                rend.material = nuevoMaterial;
            }
        }
    }
}