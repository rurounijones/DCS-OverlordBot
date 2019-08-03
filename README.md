# DCS-SimpleRadioStandalone Overlord Branch

The purpose of this branch is to try and integrate Simple Radio with Microsoft
Cognitive Services [Speech Services](https://azure.microsoft.com/en-gb/services/cognitive-services/directory/speech/) 

The aim is to enable a Voice Recognition bot to be able to parse and respond 
to radio calls sent via SRS. For example to act as an AWACS Controller.

Other parts of this project can be found at:

* https://gitlab.com/overlord-bot/tac-scribe/tree/implementation - Writes DCS
  game data to PostGIS enabled database

* https://gitlab.com/overlord-bot/bot-proof-of-concept - Listens to Mic and
  responds to bogey dope command using data in TacScribe PostGIS database

## Goals

* First goal: Have voice from SRS routed to the cognitive speech service
    and start parsing it for intents

