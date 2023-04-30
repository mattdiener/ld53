using System.Collections.Generic;
using Godot;

[Tool]
public partial class Island : Node3D
{
	enum TileType {
		Water, 
		Grass,
		Sand,
		Stone,
		Dirt,
		Snow
	}

	readonly Color[] colorsForTiles = {
		new Color(0,0,0,0), // Water -- invisible
		new Color("#339625ff"),
		new Color("#fffc97ff"),
		new Color("#444444ff"),
		new Color("#2c2210ff"),
		new Color("#ffffff"),
	};

	[Export]
	Texture2D TileData  {get {return tileData;} set {tileData = value; OnPropertyChanged();} }
	private Texture2D tileData;

	[Export]
	float Span {get {return span;} set {span = value; OnPropertyChanged();} }
	float span = 100;

	[Export]
	float TileSize {get {return tileSize;} set {tileSize = value; OnPropertyChanged();} }
	float tileSize = 10;

	[Export]
	Material TileMaterial {get{return tileMaterial;} set{tileMaterial = value; OnPropertyChanged();}}
	Material tileMaterial;

	BoxMesh boxMesh;
	List<StaticBody3D> islandStaticBodies;
	MultiMeshInstance3D multiMesh;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		CreateCubesFromTileData();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void CreateCubesFromTileData() {
		if (tileData == null || tileMaterial == null) {
			return;
		}

		boxMesh = new() {
			Material = tileMaterial,
			Size = new(1.0f, 1.0f, 1.0f)
		};

		if (multiMesh != null) {
			RemoveChild(multiMesh);
			multiMesh.QueueFree();
		}
		if (islandStaticBodies != null) {
			foreach(var sb in islandStaticBodies) {
				RemoveChild(sb);
				sb.QueueFree();

			}
		}

		var img = tileData.GetImage();
		var w = img.GetWidth();
		var h = img.GetHeight();

		int tileCount = 0;
		for (int x = 0; x < w; x++) {
			for (int z = 0; z < h; z++) {
				Color pix = img.GetPixel(x,z);
				var tileType = (TileType)pix.G8; 
				if (tileType != TileType.Water) {
					tileCount++;
				}
			}
		}

		multiMesh = new() {
			Multimesh = new() {
				Mesh = boxMesh,
				UseColors = true,
				TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			}
		};

		AddChild(multiMesh);
		multiMesh.Multimesh.InstanceCount = tileCount;

		Vector3 posScale = new(tileSize, 1.0f, tileSize);
		islandStaticBodies = new();
		int idx = 0; // index into multimesh
		for (int x = 0; x < w; x++) {
			for (int z = 0; z < h; z++) {
				Color pix = img.GetPixel(x,z);
				var tileHeight = pix.R8;
				var tileType = (TileType)pix.G8;
				if (tileType == TileType.Water) {
					continue;
				}

				multiMesh.Multimesh.SetInstanceTransform(idx, Transform3D.Identity
					.Scaled(new(tileSize, ((float)tileHeight / 255.0f) * span + 1.0f, tileSize))
					.Translated(new(tileSize * x, ((float)tileHeight / 512.0f) * span, tileSize * z)));

				multiMesh.Multimesh.SetInstanceColor(idx, colorsForTiles[(int)tileType]);

				// StaticBody
				StaticBody3D sb = new();
				sb.CollisionLayer = 0b11;

				// CollisionShape
				Vector3 boxSize = new(tileSize, ((float)tileHeight / 255.0f) * span + 1.0f, tileSize);
				Vector3 pos = new(x*tileSize, ((float)tileHeight / 512.0f)* span, z*tileSize);
				CollisionShape3D collision = new();
				BoxShape3D collisionShape = new();
				collisionShape.Size = boxSize;
				collision.Shape = collisionShape;
				collision.Position = pos;
				
				sb.AddChild(collision);
				AddChild(sb);
				islandStaticBodies.Add(sb);

				idx++;
			}
		}

		
	}

	private void OnPropertyChanged() {
		CreateCubesFromTileData();
	}
}
