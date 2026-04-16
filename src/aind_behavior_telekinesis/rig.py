from typing import Annotated, Literal, Optional, Union

import aind_behavior_services.rig.load_cells as lcc
import aind_behavior_services.rig.water_valve as wvc
from aind_behavior_services.rig import Rig, cameras, harp
from aind_behavior_services.rig import aind_manipulator as man
from pydantic import BaseModel, Field
from typing_extensions import TypeAliasType

from aind_behavior_telekinesis import __semver__


class AindManipulatorDevice(man.AindManipulator):
    """Appends a task specific configuration to the base manipulator model."""

    spout_axis: man.Axis = Field(default=man.Axis.Y1, description="Spout axis")


class RigCalibration(BaseModel):
    water_valve: wvc.WaterValveCalibration = Field(description="Water valve calibration")


class ZmqConnection(BaseModel):
    connection_string: str = Field(default="@tcp://localhost:5556")
    topic: str = Field(default="")


class Networking(BaseModel):
    zmq_publisher: ZmqConnection = Field(
        default=ZmqConnection(connection_string="@tcp://localhost:5556", topic="telekinesis"), validate_default=True
    )


class _OphysInterfaceBase(BaseModel):
    interface: str


class BergamoInterface(_OphysInterfaceBase):
    interface: Literal["bergamo"] = "bergamo"
    delay_trial: float = Field(default=0.0, ge=0, description="Arbitrary delay between start trigger and trial start")


class Slap2pInterface(_OphysInterfaceBase):
    interface: Literal["slap2p"] = "slap2p"
    delay_trial: float = Field(default=0.0, ge=0, description="Arbitrary delay between start trigger and trial start")
    delay_ready_start: float = Field(
        default=0.2, ge=0, description="Delay between the system being ready and a start signal being issued"
    )
    timeout_for_error: float = Field(
        default=5,
        ge=0,
        description="Time to wait for the ready signal to go low after start. If it doesn't, an error is raised.",
    )


OphysInterface = TypeAliasType(
    "OphysInterface",
    Annotated[Union[BergamoInterface, Slap2pInterface], Field(discriminator="interface")],
)


class AindBehaviorTelekinesisRig(Rig):
    version: Literal[__semver__] = __semver__
    triggered_camera_controller: cameras.CameraController[cameras.SpinnakerCamera] = Field(
        description="Required camera controller to triggered cameras."
    )
    harp_behavior: harp.HarpBehavior = Field(description="Harp behavior")
    harp_lickometer: harp.HarpLicketySplit = Field(description="Harp lickometer")
    harp_load_cells: Optional[lcc.LoadCells] = Field(default=None, description="Harp load cells")
    harp_clock_generator: harp.HarpWhiteRabbit = Field(description="Harp clock generator")
    harp_analog_input: Optional[harp.HarpAnalogInput] = Field(default=None, description="Harp analog input")
    harp_environment_sensor: Optional[harp.HarpEnvironmentSensor] = Field(
        default=None, description="Harp environment sensor"
    )
    manipulator: AindManipulatorDevice = Field(description="Manipulator")
    calibration: RigCalibration = Field(description="General rig calibration")
    networking: Networking = Field(default=Networking(), description="Networking settings", validate_default=True)
    ophys_interface: Optional[OphysInterface] = Field(
        default=BergamoInterface(), description="Ophys interface", validate_default=True
    )
