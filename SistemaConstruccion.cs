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

        // Buscar paredes cercanas
        Collider[] objetosCercanos = Physics.OverlapSphere(posicionBase, distanciaSnap);

        foreach (Collider col in objetosCercanos)
        {
            if (!col.CompareTag("Construido")) continue;
            if (col.gameObject == previewActual) continue;

            // Obtener la dirección de la pared existente
            Transform paredExistente = col.transform;
            Vector3 forwardParedExistente = paredExistente.forward;

            // Obtener la dirección de la pared que estamos colocando
            Quaternion rotacionActual = Quaternion.Euler(rotacionX, rotacionY, 0f);
            Vector3 forwardParedNueva = rotacionActual * Vector3.forward;

            // Calcular el producto punto para detectar si son perpendiculares
            float productoPunto = Mathf.Abs(Vector3.Dot(forwardParedExistente.normalized, forwardParedNueva.normalized));

            // Si son perpendiculares (producto punto cercano a 0)
            if (productoPunto < 0.3f)
            {
                // Obtener el borde de la pared existente
                Bounds bounds = col.bounds;
                Vector3 centroPared = bounds.center;

                // Calcular los dos bordes perpendiculares a la dirección de la pared
                Vector3 rightPared = paredExistente.right;
                Vector3 borde1 = centroPared + rightPared * (bounds.size.x * 0.5f);
                Vector3 borde2 = centroPared - rightPared * (bounds.size.x * 0.5f);

                // Determinar cuál borde está más cerca
                float distBorde1 = Vector3.Distance(new Vector3(posicionBase.x, 0, posicionBase.z),
                                                     new Vector3(borde1.x, 0, borde1.z));
                float distBorde2 = Vector3.Distance(new Vector3(posicionBase.x, 0, posicionBase.z),
                                                     new Vector3(borde2.x, 0, borde2.z));

                Vector3 bordeCercano = distBorde1 < distBorde2 ? borde1 : borde2;

                // Ajustar la posición al borde, pero offset según la rotación de la nueva pieza
                // La nueva pared debe alinearse considerando su propio ancho
                Vector3 offsetNuevaPared = rotacionActual * Vector3.right * (anchoPared * 0.5f);

                posicionFinal.x = bordeCercano.x - offsetNuevaPared.x;
                posicionFinal.z = bordeCercano.z - offsetNuevaPared.z;

                break; // Solo aplicar snap a la primera pared perpendicular encontrada
            }
        }

        return posicionFinal;
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