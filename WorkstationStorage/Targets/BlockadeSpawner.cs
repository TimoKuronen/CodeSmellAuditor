using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using WebCasuals;
using VContainer.Unity;
namespace GhoulSmasher
{
    /// <summary>
    /// Spawns pooled blockade pieces and ramps ahead of the ball.
    /// Ticked by VContainer when active; notified by EnvironmentStreamer on building spawns.
    /// </summary>
    public class BlockadeSpawner : ITickable, IBlockadeSpawner
    {
        private sealed class ActiveBlockade
        {
            public float SpawnZ;
            public bool HadRamp;
            public readonly List<BlockadePiece> Pieces = new();
            public GameObject Ramp;
        }

        private readonly BlockadeSpawnConfig config;
        private readonly DifficultyConfig difficultyConfig;
        private readonly EnvironmentConfig environmentConfig;
        private readonly RunClock runClock;
        private readonly DebugConfig debugConfig;
        private readonly Transform poolRoot;
        private readonly IRandom random;
        private readonly BlockadeSpawnPlanner spawnPlanner;

        private BallController ball;
        private ObjectPool<BlockadePiece> piecePool;
        private ObjectPool<GameObject> rampPool;
        private int piecePoolIndex;
        private int rampPoolIndex;
        private readonly List<ActiveBlockade> activeBlockades = new();
        private int leftBuildingSpawnCount;
        private int previousOpeningLane;
        private bool hasPreviousOpening;
        private bool spawnerActive;

        public BlockadeSpawner(
            BallController ballController,
            BlockadeSpawnConfig blockadeConfig,
            DifficultyConfig difficulty,
            EnvironmentConfig environment,
            RunClock clock,
            DebugConfig debug,
            GameplayPoolRoots poolRoots,
            IRandom randomSource,
            BlockadeSpawnPlanner planner)
        {
            ball = ballController;
            config = blockadeConfig;
            difficultyConfig = difficulty;
            environmentConfig = environment;
            runClock = clock;
            debugConfig = debug;
            poolRoot = poolRoots.Obstacle;
            random = randomSource;
            spawnPlanner = planner;
            BuildPools();
            ResetSpawner();
        }

        public void SetActive(bool active)
        {
            spawnerActive = active;
        }

        public void ResetSpawner()
        {
            for (int i = activeBlockades.Count - 1; i >= 0; i--)
            {
                ReleaseBlockade(activeBlockades[i]);
            }

            activeBlockades.Clear();
            leftBuildingSpawnCount = 0;
            previousOpeningLane = 0;
            hasPreviousOpening = false;
        }

        public void NotifyLeftBuildingSpawned(float buildingOriginZ)
        {
            NotifyEnvironmentSegmentSpawned(buildingOriginZ, environmentConfig.buildingSpacingZ);
        }

        public void NotifyRoadSegmentSpawned(float segmentOriginZ)
        {
            NotifyEnvironmentSegmentSpawned(segmentOriginZ, environmentConfig.roadLengthZ);
        }

        public void NotifyRoadSegmentSpawned(float segmentOriginZ, float segmentLengthZ)
        {
            NotifyEnvironmentSegmentSpawned(segmentOriginZ, segmentLengthZ);
        }

        private void NotifyEnvironmentSegmentSpawned(float segmentOriginZ, float segmentLengthZ)
        {
            if (!spawnerActive)
            {
                return;
            }

            leftBuildingSpawnCount++;
            int buildingsPerSpawn = DifficultySampler.ResolveBlockadeBuildingsPerSpawn(
                difficultyConfig,
                runClock.RunSeconds,
                config.buildingsPerSideBeforeSpawn);

            if (leftBuildingSpawnCount < buildingsPerSpawn)
            {
                return;
            }

            leftBuildingSpawnCount = 0;

            float firstBlockadeGate = DifficultySampler.ResolveFirstBlockadeMinRunSeconds(
                difficultyConfig,
                runClock.RunSeconds,
                config.firstBlockadeMinRunSeconds);
            if (runClock.RunSeconds < firstBlockadeGate)
            {
                return;
            }

            if (activeBlockades.Count >= config.maxActiveBlockades)
            {
                return;
            }

            float spawnZ = segmentOriginZ - segmentLengthZ * 0.5f;
            if (spawnZ < MinSpawnZ)
            {
                return;
            }

            if (!HasMinimumSpacingFromLatest(spawnZ))
            {
                return;
            }

            RunTimeBand band = DifficultySampler.Sample(difficultyConfig, runClock.RunSeconds);
            ActiveBlockade primaryBlockade = SpawnBlockadeAt(spawnZ, band, isPairFollowUp: false);

            if (activeBlockades.Count >= config.maxActiveBlockades)
            {
                return;
            }

            if (random.Value > Mathf.Clamp01(band.blockadePairChance))
            {
                return;
            }

            float pairMinSpacing = ResolveMinSpacingAfter(primaryBlockade);
            BlockadeSpawnPlanner.PairSpawnDecision pairDecision = spawnPlanner.ResolvePairSpawn(
                band,
                spawnZ,
                pairMinSpacing,
                MinSpawnZ,
                random);

            if (!pairDecision.ShouldSpawnPair)
            {
                return;
            }

            if (!HasMinimumSpacingFromLatest(pairDecision.PairSpawnZ))
            {
                return;
            }

            SpawnBlockadeAt(pairDecision.PairSpawnZ, band, isPairFollowUp: true);
        }

        public void Tick()
        {
            if (!spawnerActive)
            {
                return;
            }

            CleanupBehindPlayer();
        }

        private ActiveBlockade SpawnBlockadeAt(float spawnZ, RunTimeBand band, bool isPairFollowUp)
        {
            bool useRamp = spawnPlanner.ShouldUseRamp(band, config.rampVariantChance, random);
            int middleIndex = config.pieceCount / 2;
            BlockadeSpawnPlanner.OpeningChoice openingChoice = spawnPlanner.ResolveOpeningChoice(
                band,
                isPairFollowUp,
                hasPreviousOpening,
                previousOpeningLane,
                random);
            int openingLane = openingChoice.Lane;
            int variantPieceIndex = Mathf.Clamp(middleIndex + openingLane, 0, config.pieceCount - 1);

            ActiveBlockade blockade = new ActiveBlockade
            {
                SpawnZ = spawnZ,
                HadRamp = useRamp
            };

            for (int pieceIndex = 0; pieceIndex < config.pieceCount; pieceIndex++)
            {
                if (!useRamp && pieceIndex == variantPieceIndex)
                {
                    continue;
                }

                float pieceX = (pieceIndex - middleIndex) * config.pieceWidthMeters;
                Vector3 position = new Vector3(pieceX, config.spawnSurfaceY, spawnZ);
                BlockadePiece piece = piecePool.Get();
                piece.Activate(this, ball, position);
                blockade.Pieces.Add(piece);
            }

            if (useRamp)
            {
                float rampX = (variantPieceIndex - middleIndex) * config.pieceWidthMeters;
                float rampZ = spawnZ - config.rampForwardOffsetMeters;
                Vector3 rampPosition = new Vector3(rampX, config.spawnSurfaceY, rampZ);
                GameObject ramp = rampPool.Get();
                ramp.transform.SetPositionAndRotation(rampPosition, Quaternion.identity);
                if (ramp.TryGetComponent(out Rigidbody rampBody))
                {
                    rampBody.position = rampPosition;
                    rampBody.rotation = Quaternion.identity;
                }

                GameplaySceneSetup.SetLayerRecursively(ramp, GameplayLayers.Street);
                blockade.Ramp = ramp;
            }

            activeBlockades.Add(blockade);
            hasPreviousOpening = true;
            previousOpeningLane = openingLane;

            EventBus<BlockadeSpawnedEvent>.Raise(new BlockadeSpawnedEvent
            {
                SpawnZ = spawnZ,
                VariantPieceIndex = variantPieceIndex,
                OpeningLane = openingLane,
                OpeningX = (variantPieceIndex - middleIndex) * config.pieceWidthMeters,
                UseRamp = useRamp,
                IsPairFollowUp = isPairFollowUp,
                PieceCount = config.pieceCount,
                PieceWidthMeters = config.pieceWidthMeters
            });

            string variantLabel = useRamp ? "ramp" : "gap";
            string laneLabel = SpawnDecisionLog.LaneFromOffset(openingLane);
            string pairLabel = isPairFollowUp ? ", pair follow-up" : string.Empty;
            SpawnDecisionLog.Log(
                debugConfig,
                $"Blockade: {variantLabel} on {laneLabel} lane{pairLabel} — {openingChoice.Reason} (t={runClock.RunSeconds:0}s)");

            return blockade;
        }

        private bool HasMinimumSpacingFromLatest(float spawnZ)
        {
            if (!TryGetLatestActiveBlockade(out ActiveBlockade latest))
            {
                return true;
            }

            float minSpacing = ResolveMinSpacingAfter(latest);
            return spawnZ - latest.SpawnZ >= minSpacing;
        }

        private float ResolveMinSpacingAfter(ActiveBlockade previousBlockade)
        {
            return previousBlockade.HadRamp
                ? config.minBlockadeSpacingAfterRampMeters
                : config.minBlockadeSpacingMeters;
        }

        private bool TryGetLatestActiveBlockade(out ActiveBlockade latest)
        {
            latest = null;
            float bestSpawnZ = float.MinValue;

            for (int i = 0; i < activeBlockades.Count; i++)
            {
                ActiveBlockade blockade = activeBlockades[i];
                if (blockade.SpawnZ <= bestSpawnZ)
                {
                    continue;
                }

                bestSpawnZ = blockade.SpawnZ;
                latest = blockade;
            }

            return latest != null;
        }

        public void ReleasePiece(BlockadePiece piece)
        {
            for (int blockadeIndex = activeBlockades.Count - 1; blockadeIndex >= 0; blockadeIndex--)
            {
                ActiveBlockade blockade = activeBlockades[blockadeIndex];
                if (!blockade.Pieces.Remove(piece))
                {
                    continue;
                }

                piece.Deactivate();
                piece.transform.SetParent(poolRoot, false);
                piecePool.Release(piece);

                if (blockade.Pieces.Count == 0 && blockade.Ramp == null)
                {
                    activeBlockades.RemoveAt(blockadeIndex);
                }

                return;
            }
        }

        private void ReleaseBlockade(ActiveBlockade blockade)
        {
            for (int i = blockade.Pieces.Count - 1; i >= 0; i--)
            {
                BlockadePiece piece = blockade.Pieces[i];
                piece.Deactivate();
                piece.transform.SetParent(poolRoot, false);
                piecePool.Release(piece);
            }

            blockade.Pieces.Clear();

            if (blockade.Ramp != null)
            {
                blockade.Ramp.SetActive(false);
                blockade.Ramp.transform.SetParent(poolRoot, false);
                rampPool.Release(blockade.Ramp);
                blockade.Ramp = null;
            }
        }

        private void CleanupBehindPlayer()
        {
            float minZ = ball.BodyPosition.z - config.despawnBehindDistance;
            for (int i = activeBlockades.Count - 1; i >= 0; i--)
            {
                ActiveBlockade blockade = activeBlockades[i];
                if (blockade.SpawnZ < minZ)
                {
                    ReleaseBlockade(blockade);
                    activeBlockades.RemoveAt(i);
                }
            }
        }

        private float MinSpawnZ => ball.BodyPosition.z - config.despawnBehindDistance;

        private void BuildPools()
        {
            BlockadePiece piecePrefab = config.piecePrefab;
            piecePoolIndex = 0;
            piecePool = new ObjectPool<BlockadePiece>(
                createFunc: () =>
                {
                    BlockadePiece instance = Object.Instantiate(piecePrefab, poolRoot);
                    instance.name = $"{piecePrefab.name}_{piecePoolIndex:00}";
                    piecePoolIndex++;
                    instance.gameObject.SetActive(false);
                    return instance;
                },
                actionOnGet: instance => instance.gameObject.SetActive(true),
                actionOnRelease: instance =>
                {
                    instance.Deactivate();
                    instance.transform.SetParent(poolRoot, false);
                },
                actionOnDestroy: instance => Object.Destroy(instance.gameObject),
                collectionCheck: false,
                defaultCapacity: Mathf.Max(1, config.piecePoolSize),
                maxSize: Mathf.Max(32, config.piecePoolSize * 2));

            PoolPrewarmUtility.Prewarm(piecePool, config.piecePoolSize);

            GameObject rampPrefab = config.rampPrefab;
            rampPoolIndex = 0;
            rampPool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    GameObject instance = Object.Instantiate(rampPrefab, poolRoot);
                    instance.name = $"{rampPrefab.name}_{rampPoolIndex:00}";
                    rampPoolIndex++;
                    instance.SetActive(false);
                    return instance;
                },
                actionOnGet: instance => instance.SetActive(true),
                actionOnRelease: instance =>
                {
                    instance.SetActive(false);
                    instance.transform.SetParent(poolRoot, false);
                },
                actionOnDestroy: instance => Object.Destroy(instance),
                collectionCheck: false,
                defaultCapacity: Mathf.Max(1, config.rampPoolSize),
                maxSize: Mathf.Max(32, config.rampPoolSize * 2));

            PoolPrewarmUtility.Prewarm(rampPool, config.rampPoolSize);
        }
    }
}
