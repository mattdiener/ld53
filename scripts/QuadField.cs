using Godot;

[Tool]
public partial class QuadField : Node3D
{
	[Export]
	public int DetailPower { get => _detailPower; set { _detailPower = value; EditorChanged(); } }
	private int _detailPower = 1;

	// Must be in increasing order.
	[Export]
	public Godot.Collections.Array<int> DetailThresholds { get => _detailThresholds; set { _detailThresholds = value; EditorChanged(); } }
	private Godot.Collections.Array<int> _detailThresholds;

	[Export]
	public float RegionSize { get => _regionSize; set { _regionSize = value; EditorChanged(); } }
	private float _regionSize = 10f;

	[Export]
	public int RegionCountX { get => _regionCountX; set { _regionCountX = value; EditorChanged(); } }
	private int _regionCountX = 1;

	[Export]
	public int RegionCountZ { get => _regionCountZ; set { _regionCountZ = value; EditorChanged(); } }
	private int _regionCountZ = 1;

	[Export]
	public float AABBYOffset { get => _aabbYOffset; set { _aabbYOffset = value; EditorChanged(); } }
	protected float _aabbYOffset;

	[Export]
	public float AABBYScale { get => _aabbYScale; set { _aabbYScale = value; EditorChanged(); } }
	protected float _aabbYScale;

	[Export]
	public Material Material { get => _material; set { _material = value; EditorChanged(); } }
	protected Material _material;

	public Camera3D CameraOverride { get; set; }

	private int[,] _subdivisionResolutions;
	private int[,] _boundaryFlags;

	private Vector2I _virtualCurrentRegion;

	private MultiMeshInstance3D[] _subdivisonMultiMeshes;
	private float[] _uvDistancesForMeshes;
	private int[] _resolutionsAtDistances;

	private bool _isReady;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		_isReady = true;
		SetupMaterialChangedHook();
		PrecomputeResolutionsAtDistances();
		CreateSubdivisionArrays();
		CreateMultiMeshes();
		UpdateSubdivisions();
		ConfigureMultiMeshes();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta) {
		var cameraPosition = Camera()?.GlobalPosition;
		if (cameraPosition == null) {
			return;
		}

		var selfPlane = new Plane(Vector3.Up) * GlobalTransform;
		var projected = selfPlane.Project(cameraPosition.Value);
		var relativeCameraPosition = projected * GlobalTransform.Inverse();

		Vector2I virtualCurrentRegion =
			new((int)Mathf.Floor(relativeCameraPosition.X / RegionSize),
				(int)Mathf.Floor(relativeCameraPosition.Z / RegionSize));

		if (_virtualCurrentRegion != virtualCurrentRegion) {
			_virtualCurrentRegion = virtualCurrentRegion;
			UpdateSubdivisions();
		}
	}

	public void EditorChanged() {
		if (!_isReady) {
			return;
		}
		BeforeEditorChangedEvent();
		PrecomputeResolutionsAtDistances();
		CreateSubdivisionArrays();
		CreateMultiMeshes();
		UpdateSubdivisions();
		ConfigureMultiMeshes();
		AfterEditorChangedEvent();
	}

	public Vector2 Dimensions() {
		return new(_regionSize * _regionCountX, _regionSize * _regionCountZ);
	}

	protected virtual void AfterEditorChangedEvent() { }
	protected virtual void BeforeEditorChangedEvent() { }
	protected virtual void MaterialCloned(Material mat) { }

	private int ResolutionAtDistance(int distance) {
		if (distance >= _resolutionsAtDistances.Length) {
			return _resolutionsAtDistances[^1];
		}
		return _resolutionsAtDistances[distance];
	}

	private Camera3D Camera() {
		return CameraOverride ?? GetViewport().GetCamera3D();
	}

	private void PrecomputeResolutionsAtDistances() {
		int maxDistance = RegionCountX >= RegionCountZ ? RegionCountX : RegionCountZ;
		_resolutionsAtDistances = new int[maxDistance + 1];
		int thresholdStart = 0;
		int resolution = 0;

		foreach (int thresholdEnd in _detailThresholds) {
			for (int i = thresholdStart; i <= thresholdEnd && i <= maxDistance; i++) {
				_resolutionsAtDistances[i] = resolution;
			}
			resolution++;
			thresholdStart = thresholdEnd + 1;
		}

		// This suggests we didn't do anything in the foreach loop, so we're going to cheat resolution++ (TODO clean up)
		if (resolution == 0) {
			resolution++;
		}

		for (int i = thresholdStart; i <= maxDistance; i++) {
			_resolutionsAtDistances[i] = resolution - 1;
		}
	}

	private void RecalculateResolutionBoundaries() {
		for (int x = 0; x < RegionCountX; x++) {
			for (int z = 0; z < RegionCountZ; z++) {
				int boundaries = 0;

				// Notify neighbor's shaders if they need to skip every 2nd node on each edge
				if (z > 0) {
					bool shouldSet = _subdivisionResolutions[x, z - 1] > _subdivisionResolutions[x, z];
					boundaries += shouldSet ? 0b0001 : 0;
				}
				if (z < RegionCountZ - 1) {
					bool shouldSet = _subdivisionResolutions[x, z + 1] > _subdivisionResolutions[x, z];
					boundaries += shouldSet ? 0b0010 : 0;
				}
				if (x > 0) {
					bool shouldSet = _subdivisionResolutions[x - 1, z] > _subdivisionResolutions[x, z];
					boundaries += shouldSet ? 0b0100 : 0;
				}
				if (x < RegionCountX - 1) {
					bool shouldSet = _subdivisionResolutions[x + 1, z] > _subdivisionResolutions[x, z];
					boundaries += shouldSet ? 0b1000 : 0;
				}
				_boundaryFlags[x, z] = boundaries;
			}
		}
	}

	private int SetSubdivisionResolution(int x, int z, int resolution) {
		if (_subdivisionResolutions[x, z] == resolution) {
			return 0;
		}
		_subdivisionResolutions[x, z] = resolution;
		return 1;
	}

	private void CreateSubdivisionArrays() {
		_subdivisionResolutions = new int[RegionCountX, RegionCountZ];
		_boundaryFlags = new int[RegionCountX, RegionCountZ];
	}

	private void UpdateSubdivisions() {
		int changedCount = 0;
		for (int x = 0; x < RegionCountX; x++) {
			for (int z = 0; z < RegionCountZ; z++) {
				Vector2I regionVec = new(x, z);
				Vector2I absDiff = (regionVec - _virtualCurrentRegion).Abs();
				int dist = absDiff[(int)absDiff.MaxAxisIndex()];
				int resolution = ResolutionAtDistance(dist);
				changedCount += SetSubdivisionResolution(x, z, resolution);
			}
		}
		if (changedCount > 0) {
			RecalculateResolutionBoundaries();
			ConfigureMultiMeshes();
		}
	}

	private void UpdateMultiMeshParams() {
		// Set the params on those meshes
		float surfaceScaleX = 1f / RegionCountX;
		float surfaceScaleZ = 1f / RegionCountZ;
		int[] indexes = new int[_subdivisonMultiMeshes.Length];
		for (int x = 0; x < RegionCountX; x++) {
			for (int z = 0; z < RegionCountZ; z++) {
				int resolution = _subdivisionResolutions[x, z];
				int idx = indexes[resolution]++;
				Transform3D transform = Transform3D.Identity.Translated(new Vector3(x * RegionSize, 0, z * RegionSize));
				Color customData = new(x * surfaceScaleX, z * surfaceScaleZ, _boundaryFlags[x, z], 0);
				_subdivisonMultiMeshes[resolution].Multimesh.SetInstanceTransform(idx, transform);
				_subdivisonMultiMeshes[resolution].Multimesh.SetInstanceCustomData(idx, customData);
			}
		}
	}

	private void ConfigureMultiMeshes() {
		int[] counts = new int[_subdivisonMultiMeshes.Length];

		// Count the number of meshes to render for each resolution multimesh
		for (int x = 0; x < RegionCountX; x++) {
			for (int z = 0; z < RegionCountZ; z++) {
				counts[_subdivisionResolutions[x, z]]++;
			}
		}

		// Set the number of meshes
		for (int i = 0; i < counts.Length; i++) {
			_subdivisonMultiMeshes[i].Multimesh.InstanceCount = counts[i];
		}

		UpdateMultiMeshParams();
	}

	private void CreateMultiMeshes() {
		if (_subdivisonMultiMeshes != null) {
			foreach (var mesh in _subdivisonMultiMeshes) {
				RemoveChild(mesh);
			}
		}
		_uvDistancesForMeshes = new float[_detailThresholds.Count + 1];
		_subdivisonMultiMeshes = new MultiMeshInstance3D[_detailThresholds.Count + 1];
		for (int i = 0; i <= _detailThresholds.Count; i++) {
			int cutCount = (1 << (_detailPower + _detailThresholds.Count - i)) - 1;
			_uvDistancesForMeshes[i] = 1.0f / (cutCount + 1.0f);

			Material mat = null;
			if (_material != null) {
				Vector2 surfaceScale = new(1f / RegionCountX, 1f / RegionCountZ);
				mat = (Material)_material.Duplicate();
				mat.Set("shader_parameter/scale", surfaceScale);
				mat.Set("shader_parameter/unscaledVertexDistance", _uvDistancesForMeshes[i]);
				MaterialCloned(mat);
			}

			QuadMesh mesh = new() {
				SubdivideDepth = cutCount,
				SubdivideWidth = cutCount,
				Orientation = PlaneMesh.OrientationEnum.Y,
				CenterOffset = new Vector3(RegionSize / 2, 0, RegionSize / 2),
				Size = new Vector2(RegionSize, RegionSize),
				Material = mat,
			};

			_subdivisonMultiMeshes[i] = new() {
				Multimesh = new() {
					Mesh = mesh,
					UseColors = false,
					UseCustomData = true,
					TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
				},
				CustomAabb = new Aabb(Position + new Vector3(0, AABBYOffset, 0), new Vector3(_regionSize * _regionCountX, AABBYScale, _regionSize * _regionCountZ)),
				ExtraCullMargin = 100.0f,
			};

			AddChild(_subdivisonMultiMeshes[i]);
		}
	}

	private void SetupMaterialChangedHook() {
		if (_material != null) {
			_material.Changed += EditorChanged;
			if (_material is ShaderMaterial sm && sm.Shader != null) {
				sm.Shader.Changed += EditorChanged;
			}
		}
	}
}
