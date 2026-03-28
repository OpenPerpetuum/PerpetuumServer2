![opp-server2](opp-server2.png)

[![Build Perpetuum.Server Service v2](https://github.com/OpenPerpetuum/PerpetuumServer2/actions/workflows/dotnet.yml/badge.svg?branch=develop)](https://github.com/OpenPerpetuum/PerpetuumServer2/actions/workflows/dotnet.yml)

# The Open Perpetuum Server 2

## Setup (container)

This section explains how to setup for local development using docker/podman compose containers where it can be controlled using some make commands.

Here is some context for how this works:

The entry point is `compose.yml` which contains the containers, volumes and network definitions.

There are 2 volumes:
- `openperpetuum-data`: contains the original `PerpetuumServer/Data` + custom layers + generated perpetuum.ini
- `openperpetuum-db`: contains the database files for persistence

There is an environment file to control most of the configuration of this setup:
- `.env.local`

### 0. Requirements
- (optional) make (to help with docker-compose commands)
- docker/podman
- Steam: Perpetuum Dedicated server installed
- Latest gamma islands layers: https://drive.google.com/file/d/1Xp0T1K57Pv-vjgmpXMG8Iea_ec0bWYR4/view?usp=drive_link 
- Latest asset resource: https://drive.google.com/file/d/18fh8aRqMP1J7ycGBNGraFyQ31mMXZaq1/view?usp=drive_link

### 1. Clone this repository and update submodules

Start by cloning this repository
```sh
git clone https://github.com/OpenPerpetuum/PerpetuumServer2.git
```
or
```sh
git clone git@github.com:OpenPerpetuum/PerpetuumServer2.git
```

Checkout on the develop branch
```sh
git checkout develop
```

This repository contains 2 submodules.
- db: (OPDB) database migration files for each game update
- asset: (OPResource) game client resources that are fetched once the client connect to the server. Contains definition files for all game entities, translations, gfx, map data (layers), audio files, custom bot models.

You can initialize and update them with this command:
```sh
git submodule init && git submodule update
```

### 2. Update custom resources

This section must be performed if there are updates on the gamma layers or asset resource.

- Uncompress gamma layers into a temporary directory
- Copy gamma layers in `asset/lang0000/layers/GAMMA_LAYERS_NEW` (all .bin files)
- Create `custom-layers` directory
- Copy gamma layers in a new directory `custom-layers` (all .bin files) (the same one as above)
- Unarchive asset resource into a temporary directory
- Copy asset resource into `asset/lang0000` directory (gfx, sfx, textures)
- Create `perpetuum-data` directory
- Copy original PerpetuumServer/data folder into `perpetuum-data` (database, layers)


### 3. Run the server

At this point, you are ready to run the server.

Note: Take a look at the `.env.local` if you need to see what ports are used, what is the database password, or if you want to update various paths.

#### 3.1. Start

```sh
make up
```

This will build and start containers. The migration will run. Once the command exit, you might need to wait for a few minutes since the server is starting up.

You can monitor the server status with this command:
```sh
make log-server
```

If you see logs like `Unit enter to zone` or `Planthandler STOP SIGNAL received`, then the server is ready for a client to connect.

#### 3.2. Setup client with that local server
- Open the client
- Click on `Server list`
- Click on `ADD PRIVATE SERVER`
- Enter Name: `local`, Server address: `127.0.0.1:17700` (update the port to match your `SERVER_PORT` from the `.env.local` file)
- Click `OK`

#### 3.3. Connect client to the local server
- Select `local` in the server list
- Click `Connect` (this might take a few minutes to load, at this point the asset server is transfering files to the client)
- Login with the test account user: `test`, password: `test`

#### 3.4 Stop the server
```sh
make down
```

This will stop and delete the containers but keep the data and db volumes

#### 3,5 Stop and delete server data
```sh
make delete
```

This will stop and delete all data such as containers and volumes (data, db)



