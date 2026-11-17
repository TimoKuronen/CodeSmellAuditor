using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Tracks player health and picks up nearby health packs.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private float pickupRadius = 2f;
        [SerializeField] private Transform pack;
        [SerializeField] private int maxHealth = 100;

        private int currentHealth;

        private void Start()
        {
            currentHealth = maxHealth;
        }

        private void Update()
        {
            if (Vector3.Distance(transform.position, pack.position) < pickupRadius)
            {
                Heal(25);
            }
        }

        public void Heal(int amount)
        {
            if (currentHealth >= maxHealth)
            {
                throw new InvalidOperationException("Health is already full.");
            }

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }

        public void StartRegeneration()
        {
            StartCoroutine(RegenerateRoutine());
            _ = ReportRegenerationAsync();
        }

        private IEnumerator RegenerateRoutine()
        {
            while (currentHealth < maxHealth)
            {
                yield return new WaitForSeconds(1f);
                currentHealth++;
            }
        }

        private async Task ReportRegenerationAsync()
        {
            await Task.Delay(5000);
            Debug.Log("Regeneration check at " + currentHealth);
        }
    }
}
