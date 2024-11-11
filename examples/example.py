import datetime
import os

import aind_behavior_services.calibration.load_cells as lcc
import aind_behavior_services.rig as rig
import aind_behavior_telekinesis.task_logic as tl
from aind_behavior_services import db_utils as db
from aind_behavior_services.calibration.aind_manipulator import (
    AindManipulatorCalibration,
    AindManipulatorCalibrationInput,
    AindManipulatorCalibrationOutput,
    Axis,
    AxisConfiguration,
    ManipulatorPosition,
)
from aind_behavior_services.calibration.water_valve import (
    Measurement,
    WaterValveCalibration,
    WaterValveCalibrationInput,
    WaterValveCalibrationOutput,
)
from aind_behavior_services.session import AindBehaviorSessionModel
from aind_behavior_telekinesis.rig import AindManipulatorDevice, AindTelekinesisRig, RigCalibration


def mock_session() -> AindBehaviorSessionModel:
    """Generates a mock AindBehaviorSessionModel model"""
    return AindBehaviorSessionModel(
        date=datetime.datetime.now(tz=datetime.timezone.utc),
        experiment="Telekinesis",
        root_path="c://",
        subject="test",
        notes="test session",
        experiment_version="0.1.0",
        allow_dirty_repo=True,
        skip_hardware_validation=False,
        experimenter=["Foo", "Bar"],
    )


def mock_rig() -> AindTelekinesisRig:
    """Generates a mock AindVrForagingRig model"""

    manipulator_calibration = AindManipulatorCalibration(
        output=AindManipulatorCalibrationOutput(),
        input=AindManipulatorCalibrationInput(
            full_step_to_mm=(ManipulatorPosition(x=0.010, y1=0.010, y2=0.010, z=0.010)),
            axis_configuration=[
                AxisConfiguration(axis=Axis.Y1, min_limit=-1, max_limit=15000),
                AxisConfiguration(axis=Axis.Y2, min_limit=-1, max_limit=15000),
                AxisConfiguration(axis=Axis.X, min_limit=-1, max_limit=15000),
                AxisConfiguration(axis=Axis.Z, min_limit=-1, max_limit=15000),
            ],
            homing_order=[Axis.Y1, Axis.Y2, Axis.X, Axis.Z],
            initial_position=ManipulatorPosition(y1=0, y2=0, x=0, z=0),
        ),
    )

    water_valve_input = WaterValveCalibrationInput(
        measurements=[
            Measurement(valve_open_interval=1, valve_open_time=1, water_weight=[1, 1], repeat_count=200),
            Measurement(valve_open_interval=2, valve_open_time=2, water_weight=[2, 2], repeat_count=200),
        ]
    )
    water_valve_calibration = WaterValveCalibration(
        input=water_valve_input, output=water_valve_input.calibrate_output(), date=datetime.datetime.now()
    )
    water_valve_calibration.output = WaterValveCalibrationOutput(slope=1, offset=0)  # For testing purposes

    video_writer = rig.VideoWriterFfmpeg(
        frame_rate=120, container_extension="mp4", output_arguments="-c:v h264_nvenc -vsync 0 -2pass "
    )

    load_cells_calibration = lcc.LoadCellsCalibration(
        output=lcc.LoadCellsCalibrationOutput(),
        input=lcc.LoadCellsCalibrationInput(),
        date=datetime.datetime.now(),
    )
    return AindTelekinesisRig(
        rig_name="test_rig",
        triggered_camera_controller=rig.CameraController[rig.SpinnakerCamera](
            frame_rate=120,
            cameras={
                "FaceCamera": rig.SpinnakerCamera(
                    serial_number="SerialNumber", binning=1, exposure=5000, gain=0, video_writer=video_writer
                ),
                "SideCamera": rig.SpinnakerCamera(
                    serial_number="SerialNumber", binning=1, exposure=5000, gain=0, video_writer=video_writer
                ),
            },
        ),
        harp_load_cells=lcc.LoadCells(port_name="COM4", calibration=load_cells_calibration),
        monitoring_camera_controller=rig.CameraController[rig.WebCamera](cameras={"WebCam0": rig.WebCamera(index=0)}),
        harp_behavior=rig.HarpBehavior(port_name="COM3"),
        harp_lickometer=rig.HarpLickometer(port_name="COM5"),
        harp_clock_generator=rig.HarpClockGenerator(
            port_name="COM6",
            connected_clock_outputs=[]),
        harp_analog_input=None,
        manipulator=AindManipulatorDevice(port_name="COM9", calibration=manipulator_calibration),
        screen=rig.Screen(display_index=1),
        calibration=RigCalibration(water_valve=water_valve_calibration),
    )


def mock_task_logic() -> tl.AindTelekinesisTaskLogic:
    prototype_trial = tl.Action(
        reward_probability=tl.scalar_value(1),
        reward_amount=tl.scalar_value(1),
        reward_delay=tl.scalar_value(0),
        action_duration=tl.scalar_value(0.2),
        is_operant=True,
        time_to_collect=tl.scalar_value(5),
        lower_action_threshold=tl.scalar_value(0),
        upper_action_threshold=tl.scalar_value(20000),
        continuous_feedback=tl.ManipulatorFeedback(converter_lut_input=[0, 0.2, 1], converter_lut_output=[1, 5, 10]),
    )
    return tl.AindTelekinesisTaskLogic(
        task_parameters=tl.AindTelekinesisTaskParameters(
            rng_seed=None,
            environment=tl.Environment(
                block_statistics=[
                    tl.BlockGenerator(
                        block_size=tl.scalar_value(1000),
                        trial_statistics=tl.Trial(
                            inter_trial_interval=tl.scalar_value(1),
                            quiescence_period=tl.QuiescencePeriod(
                                duration=tl.scalar_value(1), has_cue=True, action_threshold=2000
                            ),
                            response_period=tl.ResponsePeriod(
                                duration=tl.scalar_value(1), has_cue=True, action=prototype_trial
                            ),
                            action_source_0=tl.LoadCellActionSource(channel=0),
                            action_source_1=tl.BehaviorAnalogInputActionSource(channel=0),
                            lut_reference="amazing_lut",
                        ),
                    )
                ],
            ),
            operation_control=tl.OperationControl(
                spout=tl.SpoutOperationControl(
                    default_retracted_position=10, default_extended_position=20, enabled=False
                ),
                action_luts={
                    "amazing_lut": tl.ActionLookUpTableFactory(
                        path="amazing_lut.tiff",
                        offset=0,
                        scale=1,
                        action0_min=0,
                        action0_max=20000,
                        action1_min=0,
                        action1_max=2048,
                    )
                },
            ),
        )
    )


def mock_subject_database() -> db.SubjectDataBase:
    """Generates a mock database object"""
    database = db.SubjectDataBase()
    database.add_subject("test", db.SubjectEntry(task_logic_target="preward_intercept_stageA"))
    database.add_subject("test2", db.SubjectEntry(task_logic_target="does_notexist"))
    return database


def main(path_seed: str = "./local/{schema}.json"):
    example_session = mock_session()
    example_rig = mock_rig()
    example_task_logic = mock_task_logic()
    example_database = mock_subject_database()

    os.makedirs(os.path.dirname(path_seed), exist_ok=True)

    models = [example_task_logic, example_session, example_rig, example_database]

    for model in models:
        with open(path_seed.format(schema=model.__class__.__name__), "w", encoding="utf-8") as f:
            f.write(model.model_dump_json(indent=2))


if __name__ == "__main__":
    main()
