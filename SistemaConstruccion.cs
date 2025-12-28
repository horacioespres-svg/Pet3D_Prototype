using UnityEngine;

public class SistemaConstruccion : MonoBehaviour
{
    [Header("Configuración")]
    public float tamañoGrid = 2.5f;
    public float distanciaSnap = 1.5f;
    public float anchoPared = 2.0f;

    [Header("Prefabs de Construcción")]
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
            Debug.Log("Modo construcción: " + (modoConstruccion ? "ACTIVADO" : "DESACTIVADO"));
        }

        if (modoConstruccion)
        {
            GestionarRotacion();
            ActualizarPreviewConSnap();

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

        if (Input.GetKeyDown(KeyCode.R)) rotacionY += 90f;
        if (Input.GetKeyDown(KeyCode.T)) rotacionX += 90f;

        if (previewActual != null)
        {
            previewActual.transform.rotation = Quaternion.Euler(rotacionX, rotacionY, 0f);
        }
    }

    void ActualizarPreviewConSnap()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit, 100f)) return;

        Vector3 posicionBase = hit.point;

        // Snap vertical si hacemos hit en algo construido
        if (hit.collider.CompareTag("Construido"))
        {
            posicionBase.y = hit.collider.bounds.max.y;
        }

        // CREAR PREVIEW si no existe
        if (previewActual == null && prefabParedActual != null)
        {
            previewActual = Instantiate(prefabParedActual, posicionBase, Quaternion.Euler(rotacionX, rotacionY, 0f));
            HacerTransparente(previewActual);
            DesactivarColliders(previewActual);
        }

        if (previewActual == null) return;

        // ============= SNAP MAGNÉTICO =============
        Vector3 posicionFinal = posicionBase;
        GameObject[] paredes = GameObject.FindGameObjectsWithTag("Construido");

        float mejorDistancia = float.MaxValue;

        Quaternion rotActual = Quaternion.Euler(rotacionX, rotacionY, 0f);
        Vector3 forwardNuevo = rotActual * Vector3.forward;
        Vector3 rightNuevo = rotActual * Vector3.right;

        foreach (GameObject pared in paredes)
        {
            if (pared == previewActual) continue;

            Renderer rend = pared.GetComponentInChildren<Renderer>();
            if (rend == null) continue;

            Bounds bounds = rend.bounds;
            Vector3 centro = bounds.center;

            // Distancia horizontal
            float dist2D = Vector3.Distance(
                new Vector3(posicionBase.x, 0, posicionBase.z),
                new Vector3(centro.x, 0, centro.z)
            );

            if (dist2D > distanciaSnap * 3f) continue;

            Vector3 forwardPared = pared.transform.forward;
            Vector3 rightPared = pared.transform.right;
            float dot = Mathf.Abs(Vector3.Dot(forwardPared, forwardNuevo));

            // PERPENDICULAR (90 grados)
            if (dot < 0.3f)
            {
                Vector3 borde1 = centro + rightPared * (bounds.size.x * 0.5f);
                Vector3 borde2 = centro - rightPared * (bounds.size.x * 0.5f);
                Vector3 offset = rightNuevo * (anchoPared * 0.5f);

                ProbarSnapPoint(borde1 - offset, posicionBase, ref mejorDistancia, ref posicionFinal);
                ProbarSnapPoint(borde2 - offset, posicionBase, ref mejorDistancia, ref posicionFinal);
            }
            // PARALELO (misma dirección)
            else if (dot > 0.7f)
            {
                Vector3 lado1 = centro - rightPared * (bounds.size.x * 0.5f);
                Vector3 lado2 = centro + rightPared * (bounds.size.x * 0.5f);
                Vector3 offset = rightNuevo * (anchoPared * 0.5f);

                ProbarSnapPoint(lado1 - offset, posicionBase, ref mejorDistancia, ref posicionFinal);
                ProbarSnapPoint(lado2 + offset, posicionBase, ref mejorDistancia, ref posicionFinal);
            }

            // VERTICAL (apilar)
            if (dist2D < anchoPared * 1.5f)
            {
                Vector3 arriba = new Vector3(centro.x, bounds.max.y, centro.z);
                float distV = Vector3.Distance(posicionBase, arriba);
                if (distV < mejorDistancia)
                {
                    mejorDistancia = distV;
                    posicionFinal = arriba;
                }
            }
        }

        // Aplicar snap si está cerca
        if (mejorDistancia < distanciaSnap)
        {
            previewActual.transform.position = posicionFinal;
            Debug.Log("✓ SNAP ACTIVADO - Distancia: " + mejorDistancia.ToString("F2") + "m");
        }
        else
        {
            previewActual.transform.position = posicionBase;
        }

        puedeColocar = !HayColision();
        CambiarColorPreview(puedeColocar ? materialVerde : materialRojo);
    }

    void ProbarSnapPoint(Vector3 punto, Vector3 cursor, ref float mejorDist, ref Vector3 mejorPunto)
    {
        punto.y = cursor.y;
        float dist = Vector3.Distance(
            new Vector3(cursor.x, 0, cursor.z),
            new Vector3(punto.x, 0, punto.z)
        );

        if (dist < mejorDist)
        {
            mejorDist = dist;
            mejorPunto = punto;
        }
    }

    bool HayColision()
    {
        if (previewActual == null) return false;

        Renderer[] renderers = previewActual.GetComponentsInChildren<Renderer>();

        foreach (Renderer rend in renderers)
        {
            Bounds bounds = rend.bounds;
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
