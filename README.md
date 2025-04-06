# Unity Procedural Content Generation (PCG) Plugin

## Overview
The Unity PCG Plugin is designed to facilitate the creation of procedurally generated levels for top-down shooter games. This plugin allows developers to customize various aspects of level generation, including the number of levels, rooms, tiles, enemies, players, decorators, and triggers for game start and end events.

## Features
- **Customizable Level Generation**: Define the number of levels, rooms, and tiles to create unique gameplay experiences.
- **Enemy and Player Spawning**: Easily customize enemy types, spawn rates, and player start positions.
- **Decorative Elements**: Enhance the visual appeal of levels with customizable decorative elements.
- **Triggers for Game Events**: Implement start and end triggers to manage game flow and events.
- **User-Friendly Editor**: A custom editor interface for easy configuration of level generation settings directly from the Unity Inspector.

## Getting Started
1. **Installation**: Import the Unity PCG Plugin into your Unity project.
2. **Setup**: Open the `DemoScene.unity` located in the `Assets/Scenes` folder to see the plugin in action.
3. **Configuration**: Use the custom editor in the `Assets/Editor/PCGEditor.cs` to configure your level generation settings.
4. **Generate Levels**: Click the "Generate Level" button in the Inspector to create your levels based on the defined parameters.

## Usage
- **LevelGenerator**: Manages the overall level generation process.
- **RoomGenerator**: Handles the creation of individual rooms and their connections.
- **TileGenerator**: Manages tile placement within rooms.
- **EnemySpawner**: Customizes enemy spawning behavior.
- **PlayerSpawner**: Configures player spawn positions.
- **DecoratorSpawner**: Places decorative elements in the levels.
- **StartTrigger**: Activates events when the game begins.
- **EndTrigger**: Triggers events when the game ends.

## Customization
The plugin allows for extensive customization through the `PCGSettings.asset` file located in `Assets/Resources/Configurations`. Modify this asset to adjust various parameters related to level generation, such as room sizes, enemy types, and decoration options.

## License
This project is licensed under the MIT License. See the LICENSE file for more details.

## Contributing
Contributions are welcome! Please feel free to submit issues or pull requests to enhance the functionality of the Unity PCG Plugin.

## Contact
For support or inquiries, please contact the project maintainer at [your-email@example.com].