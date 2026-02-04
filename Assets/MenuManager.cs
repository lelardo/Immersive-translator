using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // Necesario para acceder al Dropdown

public class MenuManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Dropdown dropdownIdioma; // Arrastra aquí el Dropdown de Idiomas
    public TMP_Dropdown dropdownModo;   // <--- NUEVO: Arrastra aquí el Dropdown de Modos

    // Esta función se llamará al pulsar el botón "Iniciar"
    public void IniciarExperiencia()
    {
        Debug.Log("1. Botón presionado. Iniciando lógica...");

        // --- VALIDACIONES DE SEGURIDAD ---
        if (dropdownIdioma == null)
        {
            Debug.LogError("¡ERROR CRÍTICO! El 'Dropdown Idioma' no está asignado en el Inspector.");
            return;
        }

        if (dropdownModo == null)
        {
            Debug.LogError("¡ERROR CRÍTICO! El 'Dropdown Modo' no está asignado en el Inspector.");
            return; 
        }

        // --- 1. LÓGICA DE IDIOMA ---
        int indiceIdioma = dropdownIdioma.value;
        string codigoIdioma = "es";

        // Ajusta esto al orden visual de tu lista
        switch (indiceIdioma)
        {
            case 0: codigoIdioma = "es"; break; // Español
            case 1: codigoIdioma = "en"; break; // Inglés
            case 2: codigoIdioma = "fr"; break; // Francés
            case 3: codigoIdioma = "qu"; break; // Kichwa
            default: codigoIdioma = "es"; break;
        }

        // --- 2. LÓGICA DE MODO (NUEVO) ---
        // Opción 0: Traducción Literal
        // Opción 1: Adaptación Natural
        int indiceModo = dropdownModo.value;
        string codigoModo = "Traduccion"; 

        switch (indiceModo)
        {
            case 0:
                codigoModo = "Traduccion"; // Le dice al TraductorTexto que use Google
                break;
            case 1:
                codigoModo = "Adaptacion"; // Le dice al TraductorTexto que use Gemini
                break;
        }

        // --- 3. GUARDAR CONFIGURACIÓN ---
        PlayerPrefs.SetString("IdiomaDestino", codigoIdioma);
        PlayerPrefs.SetString("ModoApp", codigoModo); 
        PlayerPrefs.Save(); // Forzar guardado inmediato

        Debug.Log($"2. Configuración guardada: Idioma [{codigoIdioma}] - Modo [{codigoModo}]. Cargando AR...");

        // --- 4. CAMBIAR ESCENA ---
        // Asegúrate que tu escena AR es la número 1 en Build Settings
        SceneManager.LoadScene(1); 
    }
}