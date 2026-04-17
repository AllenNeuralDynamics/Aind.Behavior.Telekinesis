import datetime
import os

from aind_behavior_services.rig import cameras, harp
from aind_behavior_services.rig.aind_manipulator import (
    AindManipulatorCalibration,
    Axis,
    AxisConfiguration,
    ManipulatorPosition,
    MotorOperationMode,
)
from aind_behavior_services.rig.water_valve import Measurement, calibrate_water_valves
from aind_behavior_services.session import Session

import aind_behavior_telekinesis.task_logic as tl
from aind_behavior_telekinesis.rig import (
    AindBehaviorTelekinesisRig,
    AindManipulatorDevice,
    Networking,
    RigCalibration,
    ZmqConnection,
)


def mock_session() -> Session:
    """Generates a mock Session model"""
    return Session(
        date=datetime.datetime.now(tz=datetime.timezone.utc),
        experiment="Telekinesis",
        subject="test",
        notes="test session",
        allow_dirty_repo=True,
        skip_hardware_validation=False,
        experimenter=["Foo", "Bar"],
    )


def mock_rig() -> AindBehaviorTelekinesisRig:
    """Generates a mock AindVrForagingRig model"""

    manipulator_calibration = AindManipulatorCalibration(
        full_step_to_mm=(ManipulatorPosition(x=0.010, y1=0.010, y2=0.010, z=0.010)),
        axis_configuration=[
            AxisConfiguration(axis=Axis.Y1, min_limit=-1, max_limit=30, motor_operation_mode=MotorOperationMode.QUIET),
            AxisConfiguration(axis=Axis.X, min_limit=-1, max_limit=30),
            AxisConfiguration(axis=Axis.Z, min_limit=-1, max_limit=30),
        ],
        homing_order=[Axis.Y1, Axis.X, Axis.Z],
        initial_position=ManipulatorPosition(y1=0, y2=0, x=0, z=0),
    )

    measurements = [
        Measurement(valve_open_interval=1, valve_open_time=1, water_weight=[1, 1], repeat_count=200),
        Measurement(valve_open_interval=2, valve_open_time=2, water_weight=[2, 2], repeat_count=200),
    ]
    water_valve_calibration = calibrate_water_valves(measurements)
    water_valve_calibration.slope = 1  # for simplicity if you dont want to calibrate. Volume = Time...
    water_valve_calibration.offset = 0

    video_writer = cameras.VideoWriterFfmpeg()

    return AindBehaviorTelekinesisRig(
        rig_name="BCI_Bonsai_i",
        computer_name="test_computer",
        data_directory="C:/data",
        triggered_camera_controller=cameras.CameraController[cameras.SpinnakerCamera](
            frame_rate=25,
            cameras={
                "MainCamera": cameras.SpinnakerCamera(
                    serial_number="23381093",
                    binning=2,
                    exposure=10000,
                    gain=20,
                    video_writer=video_writer,
                    adc_bit_depth=None,
                )
            },
        ),
        harp_load_cells=None,
        harp_behavior=harp.HarpBehavior(port_name="COM4"),
        harp_lickometer=harp.HarpLicketySplit(port_name="COM6"),
        harp_clock_generator=harp.HarpWhiteRabbit(port_name="COM7"),
        harp_analog_input=None,
        manipulator=AindManipulatorDevice(port_name="COM5", calibration=manipulator_calibration, spout_axis=Axis.Y1),
        calibration=RigCalibration(water_valve=water_valve_calibration),
        networking=Networking(
            zmq_publisher=ZmqConnection(connection_string="@tcp://localhost:5556", topic="Telekinesis")
        ),
    )


def mock_task_logic() -> tl.AindBehaviorTelekinesisTaskLogic:
    prototype_trial = tl.Action(
        reward_probability=tl.scalar_value(1),
        reward_amount=tl.scalar_value(5),
        reward_delay=tl.scalar_value(0),
        action_duration=tl.scalar_value(0),
        is_operant=True,
        time_to_collect=tl.scalar_value(2),
        lower_action_threshold=tl.scalar_value(0),
        upper_action_threshold=tl.scalar_value(2),
        continuous_feedback=tl.ManipulatorFeedback(
            converter_lut_output=[0, -7]
        ),  # This is in real manipulator units, with 0 being the reference position and -7 being the fully retracted position
    )
    return tl.AindBehaviorTelekinesisTaskLogic(
        task_parameters=tl.AindTelekinesisTaskParameters(
            rng_seed=None,
            environment=tl.Environment(
                block_statistics=[
                    tl.BlockGenerator(
                        block_size=tl.scalar_value(1000),
                        trial_statistics=tl.Trial(
                            inter_trial_interval=tl.scalar_value(10),
                            quiescence_period=tl.QuiescencePeriod(
                                duration=0.2, action_threshold=0.02
                            ),  # 2% of the full action range
                            response_period=tl.ResponsePeriod(
                                duration=tl.scalar_value(10), has_cue=True, action=prototype_trial
                            ),
                            action_source_0=tl.BehaviorAnalogInputActionSource(channel=0),
                            sampler=tl.Sampler1D(min_from=0, max_from=3.3, min_to=0, max_to=1),
                        ),
                    )
                ],
            ),
            operation_control=tl.OperationControl(
                spout=tl.SpoutOperationControl(default_retraction_offset=-7, enabled=True),
            ),
        )
    )


def main(path_seed: str = "./local/{schema}.json"):
    example_task_logic = mock_task_logic()
    example_session = mock_session()
    example_rig = mock_rig()

    os.makedirs(os.path.dirname(path_seed), exist_ok=True)

    models = [example_task_logic, example_session, example_rig]

    for model in models:
        with open(path_seed.format(schema=model.__class__.__name__), "w", encoding="utf-8") as f:
            f.write(model.model_dump_json(indent=2))


if __name__ == "__main__":
    main()
