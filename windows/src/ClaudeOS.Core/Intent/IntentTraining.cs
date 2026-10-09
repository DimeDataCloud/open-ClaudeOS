namespace ClaudeOS.Core.Intent;

/// <summary>
/// What <see cref="LocalIntentClassifier"/> learns from: short phrases people actually say, grouped
/// by what they want. They are deliberately phrasings the grammar does not already parse, because the
/// grammar answers those first. Open means "show me something that exists", find means "work out where
/// it is", make means "create something new", change means "edit what is on screen", window means
/// "move or resize windows", and other is everything for the planner or the cloud: chat, questions, and
/// tasks that act on files. Add examples here when a phrasing is routed wrongly.
/// </summary>
public static class IntentTraining
{
    public static IReadOnlyList<(IntentLabel Label, string Phrase)> Examples { get; } = Parse(Data);

    private static List<(IntentLabel, string)> Parse(string data)
    {
        var examples = new List<(IntentLabel, string)>();
        foreach (var line in data.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            examples.Add((Enum.Parse<IntentLabel>(line[..colon], ignoreCase: true), line[(colon + 1)..].Trim()));
        }

        return examples;
    }

    private const string Data = """
        open: i need to see the budget spreadsheet
        open: bring up last week's meeting notes
        open: let me look at the contract from acme
        open: get me the quarterly report pdf
        open: can i see my resume
        open: i want to read the onboarding doc
        open: take me to my screenshots folder
        open: the invoice from september, bring it up
        open: load the design brief
        open: jump to my downloads
        open: i'd like to look at the tax return
        open: fetch the latest presentation
        open: get the q3 numbers up on screen
        open: let me read that article i saved
        open: reopen the document i just closed
        open: take a look at my notes
        open: my passport scan please
        open: pull the lease agreement
        open: i want to check the sales deck
        open: put the project plan on screen
        open: bring back the spreadsheet i was editing yesterday
        open: the photo from the offsite
        open: go into my documents
        open: the budget file, thanks
        open: could i get the meeting agenda
        open: view the quarterly forecast
        open: i'm after the signed contract
        open: give me the roadmap doc
        open: let's look at the wireframes
        open: load up the family photos
        open: get the handbook open
        open: the weekly report, the latest one
        find: i can't remember where the invoice went
        find: any files mentioning acme
        find: which document talks about the lease
        find: track down the file with the wifi password
        find: is there a spreadsheet about hiring
        find: search my files for tax receipts
        find: i'm looking for the photo of the whiteboard
        find: have i got anything on the merger
        find: locate every pdf from last month
        find: files modified yesterday
        find: what did i download this morning
        find: lost the contract, can you find it
        find: anything from sarah about the launch
        find: where would the budget have ended up
        find: which folder has my old resumes
        find: hunt down the notes from the board meeting
        find: do i have a copy of the offer letter
        find: look through my documents for invoices
        find: all the images from last weekend
        find: where have i stored the receipts
        find: show every file that mentions renewal
        find: is the scan of my id somewhere
        find: seek out the latest draft
        find: whatever i saved about visas
        find: which pdfs are bigger than ten megabytes
        find: the file i touched just before lunch
        make: turn these numbers into something visual
        make: i'd love a quick picture of where the money went
        make: put together a one page summary of the meeting
        make: build me a little clock for the corner of my screen
        make: i want a panel that shows my battery
        make: come up with a calmer colour scheme
        make: illustrate how the pipeline works
        make: break down sales by region visually
        make: compare this quarter against last quarter in a picture
        make: draft a status update from these notes
        make: lay out the org structure for me
        make: give me a breakdown of expenses by category
        make: whip something up with last quarter's numbers
        make: i need a dashboard for my weekly metrics
        make: sketch the architecture of this service
        make: create a countdown to friday
        make: make me a mini calendar that sits in the corner
        make: show spending over time as a line
        make: a small widget with my next meeting and the weather
        make: produce a timeline of the project
        make: could you create a one pager on the new policy
        make: generate a table of expenses by vendor
        make: i want to see revenue trends in a graph
        make: write up the findings from these invoices
        make: draw how the signup flow works
        make: a darker theme with bigger text
        make: design a layout for my morning routine
        make: compile a short brief of today's emails
        make: build a tracker for my reading list
        make: a visual of headcount by team
        make: chart this
        make: summarise what happened this week
        make: put the totals in a neat table
        make: make a leaderboard from this sheet
        change: the labels are too small
        change: can the bars be thicker
        change: lose the gridlines
        change: bump the font up a notch
        change: swap the axes
        change: i'd rather see it as a line
        change: put the legend on the left
        change: tone down the colours
        change: that title is too long
        change: show only the top five
        change: start the axis at zero
        change: round those numbers
        change: make the bars blue
        change: add a legend
        change: sort it by total
        change: rename the title to q3 spend
        change: use a darker background
        change: thinner lines please
        change: add the units to the axis
        change: hide the title
        change: bigger text on the clock
        change: make it greener
        change: group them by month instead
        change: less padding around it
        change: put the numbers on the bars
        change: remove the legend
        change: can you flip the order
        change: only show this year
        window: get this out of my way
        window: tuck this off to the side
        window: i need more room on the left
        window: line these two windows up side by side
        window: shrink this window a bit
        window: bring my windows back to how they were
        window: flip this to the other monitor
        window: cover the whole screen with this
        window: keep this visible while i work
        window: stack my windows neatly
        window: hide everything except this
        window: make this window smaller
        window: split the screen between these two
        window: move this over to my second screen
        window: send this to the back
        window: put this window in the corner
        window: tile everything
        window: give this more space
        window: minimise everything else
        window: stop this covering my other window
        other: what's the weather like tomorrow
        other: tell me a joke
        other: who won the game last night
        other: how do i convert celsius to fahrenheit
        other: explain how transformers work
        other: what time is it in tokyo
        other: remind me to call mom
        other: set an alarm for seven
        other: play some music
        other: translate good morning into french
        other: how are you today
        other: thanks that was great
        other: email the total to finance
        other: rename all the invoices by date
        other: organise my downloads folder
        other: move the pdfs into folders by year
        other: delete the old screenshots
        other: send the report to my manager
        other: back up my documents
        other: clean up my desktop
        other: what is the capital of australia
        other: why is the sky blue
        other: write a poem about autumn
        other: how many calories are in an avocado
        other: help
        other: what can you do
        other: who are you
        other: summarise the invoices into a spreadsheet and email finance
        other: schedule a meeting with sarah
        other: what's two plus two
        other: book me a flight
        other: how do i fix a flat tire
        other: recommend a good book
        other: what is the meaning of life
        other: turn off the lights
        other: call a taxi
        other: what's on my calendar tomorrow
        other: copy this folder to a usb drive
        other: zip up the project and send it
        other: tell me about the roman empire
        """;
}
