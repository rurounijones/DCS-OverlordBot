# Sorts the Airfields.json file so that additions result in minimal
# line noise. Run from the same directory as Airfields.json
# Requires Ruby
# gem install json

require "json"

AIRFIELDS_FILE = "Airfields.json"

puts "Sorting #{AIRFIELDS_FILE}"

existing_json = JSON.parse(File.read AIRFIELDS_FILE)
sorted_json = []

# We create a new hash to make sure the hash fields are sorted
# the way we want them since Hashes follow insertion order and the
# existing json might have had the fields in a different order.
existing_json.each do |airfield|
  sorted_json << {
    "name" => airfield["name"],
    "lat" => airfield["lat"],
    "lon" => airfield["lon"],
    "alt" => airfield["alt"]
  }

  puts "Sorted #{airfield["name"]}"

end

sorted_json = sorted_json.sort_by { |hsh| hsh["name"] }
puts "All airfields sorted"

File.write AIRFIELDS_FILE, JSON.pretty_generate(sorted_json)
puts "#{AIRFIELDS_FILE} updated"
