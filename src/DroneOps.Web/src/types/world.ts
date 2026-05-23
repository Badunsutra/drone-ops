export interface DroneViewDto {
  id: string;
  x: number;
  y: number;
  batteryPercent: number;
  status: string;
  currentMissionId: string | null;
}

export interface MissionViewDto {
  id: string;
  pickupX: number;
  pickupY: number;
  dropoffX: number;
  dropoffY: number;
  priority: string;
  status: string;
}

export interface EventViewDto {
  kind: string;
  message: string;
  tick: number | null;
}

export interface WorldSnapshotDto {
  tick: number;
  drones: DroneViewDto[];
  missions: MissionViewDto[];
  recentEvents: EventViewDto[];
}

export interface ObstacleDto {
  x: number;
  y: number;
}

export interface ChargingStationDto {
  id: string;
  x: number;
  y: number;
}

export interface InitialWorldDto {
  width: number;
  height: number;
  obstacles: ObstacleDto[];
  stations: ChargingStationDto[];
}
