json-schema
-------------
The following json-schemas are used as the format definition of the input for this task. They are the result of the `Pydantic`` models defined in `src/aind_behavior_telekinesis`, and are also used to generate `src/Extensions/AindBehaviorTelekinesis.cs` via `Bonsai.Sgen`.

`Download Schema <https://raw.githubusercontent.com/AllenNeuralDynamics/Aind.Behavior.Telekinesis/main/src/DataSchemas/aind_behavior_telekinesis.json>`_

Task Logic Schema
~~~~~~~~~~~~~~~~~
.. jsonschema:: https://raw.githubusercontent.com/AllenNeuralDynamics/Aind.Behavior.Telekinesis/main/src/DataSchemas/aind_behavior_telekinesis.json#/$defs/AindBehaviorTelekinesisTaskLogic
   :lift_definitions:
   :auto_reference:


Rig Schema
~~~~~~~~~~~~~~
.. jsonschema:: https://raw.githubusercontent.com/AllenNeuralDynamics/Aind.Behavior.Telekinesis/main/src/DataSchemas/aind_behavior_telekinesis.json#/$defs/AindBehaviorTelekinesisRig
   :lift_definitions:
   :auto_reference:
