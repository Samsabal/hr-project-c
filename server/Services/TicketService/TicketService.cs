using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using server.Services.AuthService;

namespace server.Services.TicketService
{
  public class TicketService : ITicketService
  {
    private readonly IMapper _mapper;
    private readonly DataContext _context;
    private readonly IAuthService _authService;

    public TicketService(IMapper mapper, DataContext context, IAuthService authService)
    {
      _mapper = mapper;
      _context = context;
      _authService = authService;
    }

    public async Task<GetTicketDTO> CreateGetTicketDTO(Ticket ticket)
    {
      var ticketDTO = _mapper.Map<GetTicketDTO>(ticket);
      ticketDTO.Creator = _mapper.Map<GetTicketUserDTO>(await _context.Users.FirstOrDefaultAsync(u => u.Id == ticket.CreatorId));
      ticketDTO.Assignee = _mapper.Map<GetTicketUserDTO>(await _context.Users.FirstOrDefaultAsync(u => u.Id == ticket.AssigneeId));
      ticketDTO.Company = _mapper.Map<GetCompanyDTO>(await _context.Companies.FirstOrDefaultAsync(c => c.Id == ticket.CompanyId));

      var ticketMachine = await _context.CompanyMachines.FirstOrDefaultAsync(cm => cm.CompanyId == ticket.CompanyId && cm.Id == ticket.CompanyMachineId);
      ticketDTO.MachineName = ticketMachine != null ? ticketMachine.Name : null;

      return ticketDTO;
    }

    public async Task<ServiceResponse<GetTicketDTO>> CreateTicket(CreateTicketDTO newTicket)
    {
      ServiceResponse<GetTicketDTO> serviceResponse = new ServiceResponse<GetTicketDTO>();
      try
      {
        System.Console.WriteLine("TICKET MACHINE: " + newTicket.CompanyMachineId);
        var ticket = _mapper.Map<Ticket>(newTicket);
        bool noExistingTickets = _context.Tickets.Count() == 0;

        ticket.Id = Guid.NewGuid();
        ticket.TicketNumber = noExistingTickets ? 1 : _context.Tickets.Max(t => t.TicketNumber) + 1;
        ticket.CreationDate = DateTime.UtcNow;
        ticket.Status = Status.Open;
        ticket.Priority = Priority.Medium;

        try
        {
          serviceResponse.Data = await CreateGetTicketDTO(ticket);
        }
        catch (Exception ex)
        {
          serviceResponse.Success = false;
          serviceResponse.Message = "Unable to return the ticket that was just created.";
          System.Console.WriteLine(ex.Message);
          return serviceResponse;
        }

        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();
      }
      catch (Exception ex)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "Unable to create ticket with given data.";
        System.Console.WriteLine(ex.Message);
      }

      // TODO: Stuur een notificatie naar alle viscon medewerkers
      return serviceResponse;
    }

    public async Task<ServiceResponse<GetTicketDTO>> GetTicketById(Guid id)
    {
      ServiceResponse<GetTicketDTO> serviceResponse = new ServiceResponse<GetTicketDTO>();
      var requestUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == _authService.GetUserEmail());

      try
      {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null)
        {
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket not found.";
          return serviceResponse;
        }

        if (requestUser == null)
        {
          serviceResponse.Success = false;
          serviceResponse.Message = "Requesting user not found.";
          return serviceResponse;
        }

        if (!(requestUser.CompanyId == ticket.CompanyId || (requestUser.Role == Role.VisconAdmin || requestUser.Role == Role.VisconEmployee)))
        {
          serviceResponse.Success = false;
          serviceResponse.Message = "You are not authorized to view this ticket.";
          return serviceResponse;
        }

        serviceResponse.Data = await CreateGetTicketDTO(ticket);
      }
      catch (Exception ex)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "Unable to get ticket with given id.";
        System.Console.WriteLine(ex.Message);
      }

      return serviceResponse;
    }

    public async Task<ServiceResponse<int>> GetTotalTickets(Status? status)
    {
      ServiceResponse<int> serviceResponse = new ServiceResponse<int>();
      var requestUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == _authService.GetUserEmail());

      try
      {
        if (requestUser == null)
        {
          serviceResponse.Success = false;
          serviceResponse.Message = "Requesting user not found.";
          return serviceResponse;
        }

        if (status != null)
        {
          if (requestUser.Role == Role.VisconAdmin || requestUser.Role == Role.VisconEmployee)
          {
            serviceResponse.Data = _context.Tickets.Where(t => t.Status == status).Count();
          }
          else
          {
            serviceResponse.Data = _context.Tickets.Where(t => t.Status == status && t.CompanyId == requestUser.CompanyId).Count();
          }
        }
        else
        {
          if (requestUser.Role == Role.VisconAdmin || requestUser.Role == Role.VisconEmployee)
          {
            serviceResponse.Data = _context.Tickets.Count();
          }
          else
          {
            serviceResponse.Data = _context.Tickets.Where(t => t.CompanyId == requestUser.CompanyId).Count();
          }
        }
      }
      catch (Exception ex)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "Unable to get total tickets.";
        System.Console.WriteLine(ex.Message);
      }

      return serviceResponse;
    }

    public async Task<ServiceResponse<List<GetTicketDTO>>> GetAllTickets(int page, Status? status)
    {
      ServiceResponse<List<GetTicketDTO>> serviceResponse = new ServiceResponse<List<GetTicketDTO>>();
      var requestUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == _authService.GetUserEmail());

      int pageSize = 10;
      int skip = (page - 1) * pageSize;

      try
      {
        if (requestUser == null)
        {
          serviceResponse.Success = false;
          serviceResponse.Message = "Requesting user not found.";
          return serviceResponse;
        }

        IQueryable<Ticket> tickets = _context.Tickets.Select(ticket => ticket);

        if (status != null)
          tickets = tickets.Where(ticket => ticket.Status == status);

        if (requestUser.Role != Role.VisconAdmin && requestUser.Role != Role.VisconEmployee)
          tickets = tickets.Where(ticket => ticket.CompanyId == requestUser.CompanyId);

        tickets = tickets
            .OrderBy(ticket => ticket.Status)
            .ThenBy(ticket => ticket.Priority)
            .ThenByDescending(ticket => ticket.CreationDate)
            .Skip(skip)
            .Take(pageSize)
            .Select(ticket => ticket);


        List<Ticket> ticketsList = await tickets.ToListAsync();
        List<GetTicketDTO> ticketsListConverted = new List<GetTicketDTO>();
        try
        {
          foreach (Ticket ticket in ticketsList)
            ticketsListConverted.Add(await CreateGetTicketDTO(ticket));
        }
        catch (Exception ex)
        {
          serviceResponse.Success = false;
          serviceResponse.Message = "Unable to return the tickets.";
          System.Console.WriteLine(ex.Message);
          return serviceResponse;
        }

        serviceResponse.Data = ticketsListConverted;
      }
      catch (Exception ex)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "Unable to get all tickets.";
        System.Console.WriteLine(ex.Message);
      }

      return serviceResponse;
    }

    public async Task<ServiceResponse<GetTicketDTO>> ClaimTicket(TicketIdDTO ticketToClaim)
    {
      ServiceResponse<GetTicketDTO> serviceResponse = new ServiceResponse<GetTicketDTO>();
      var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == _authService.GetUserEmail());

      System.Console.WriteLine("Claiming ticket with id: " + ticketToClaim.TicketId);

      if (user == null)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "User not found.";
        return serviceResponse;
      }

      try
      {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketToClaim.TicketId);
        if (ticket == null)
        {
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket not found.";
          return serviceResponse;
        }
        if (ticket.AssigneeId != null)
        {
          serviceResponse.Data = await CreateGetTicketDTO(ticket);
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket already claimed.";
          return serviceResponse;
        }
        if (ticket.Status == Status.Cancelled)
        {
          serviceResponse.Data = await CreateGetTicketDTO(ticket);
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket is cancelled.";
          return serviceResponse;
        }

        ticket.AssigneeId = user.Id;
        ticket.Status = Status.InProgress;
        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync();

        serviceResponse.Data = await CreateGetTicketDTO(ticket);
      }
      catch (Exception ex)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "Unable to claim ticket with given id.";
        System.Console.WriteLine(ex.Message);
      }

      return serviceResponse;
    }

    public async Task<ServiceResponse<GetTicketDTO>> UnclaimTicket(TicketIdDTO ticketToUnclaim)
    {
      ServiceResponse<GetTicketDTO> serviceResponse = new ServiceResponse<GetTicketDTO>();
      var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == _authService.GetUserEmail());

      System.Console.WriteLine("Unclaiming ticket with id: " + ticketToUnclaim.TicketId);

      if (user == null)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "User not found.";
        return serviceResponse;
      }

      try
      {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketToUnclaim.TicketId);
        if (ticket == null)
        {
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket not found.";
          return serviceResponse;
        }
        if (ticket.AssigneeId == null)
        {
          serviceResponse.Data = await CreateGetTicketDTO(ticket);
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket has not been claimed yet.";
          return serviceResponse;
        }
        if (ticket.Status == Status.Resolved)
        {
          serviceResponse.Data = await CreateGetTicketDTO(ticket);
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket has already been resolved.";
          return serviceResponse;
        }
        if (ticket.Status == Status.Cancelled)
        {
          serviceResponse.Data = await CreateGetTicketDTO(ticket);
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket has been cancelled.";
          return serviceResponse;
        }

        ticket.AssigneeId = null;
        ticket.Status = Status.Open;
        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync();

        serviceResponse.Data = await CreateGetTicketDTO(ticket);
      }
      catch (Exception ex)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "Unable to unclaim ticket with given id.";
        System.Console.WriteLine(ex.Message);
      }

      return serviceResponse;
    }

    public async Task<ServiceResponse<GetTicketDTO>> ResolveTicket(TicketIdDTO ticketToResolve)
    {
      ServiceResponse<GetTicketDTO> serviceResponse = new ServiceResponse<GetTicketDTO>();
      var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == _authService.GetUserEmail());

      System.Console.WriteLine("Resolving ticket with id: " + ticketToResolve.TicketId);

      if (user == null)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "User not found.";
        return serviceResponse;
      }

      try
      {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketToResolve.TicketId);
        if (ticket == null)
        {
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket not found.";
          return serviceResponse;
        }
        if (ticket.AssigneeId == null)
        {
          serviceResponse.Data = await CreateGetTicketDTO(ticket);
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket has not been claimed yet.";
          return serviceResponse;
        }
        if (ticket.Status == Status.Resolved)
        {
          serviceResponse.Data = await CreateGetTicketDTO(ticket);
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket has already been resolved.";
          return serviceResponse;
        }
        if (ticket.Status == Status.Cancelled)
        {
          serviceResponse.Data = await CreateGetTicketDTO(ticket);
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket has been cancelled.";
          return serviceResponse;
        }

        ticket.Status = Status.Resolved;
        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync();

        serviceResponse.Data = await CreateGetTicketDTO(ticket);
      }
      catch (Exception ex)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "Unable to resolve ticket with given id.";
        System.Console.WriteLine(ex.Message);
      }

      return serviceResponse;
    }

    public async Task<ServiceResponse<GetTicketDTO>> OpenTicket(TicketIdDTO ticketToOpen)
    {

      ServiceResponse<GetTicketDTO> serviceResponse = new ServiceResponse<GetTicketDTO>();
      var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == _authService.GetUserEmail());

      System.Console.WriteLine("Resolving ticket with id: " + ticketToOpen.TicketId);

      if (user == null)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "User not found.";
        return serviceResponse;
      }

      try
      {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketToOpen.TicketId);
        if (ticket == null)
        {
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket not found.";
          return serviceResponse;
        }
        if (ticket.Status == Status.Cancelled)
        {
          serviceResponse.Data = await CreateGetTicketDTO(ticket);
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket has been cancelled.";
          return serviceResponse;
        }
        if (ticket.AssigneeId == null)
        {
          serviceResponse.Data = await CreateGetTicketDTO(ticket);
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket has not been claimed yet.";
          return serviceResponse;
        }
        if (ticket.Status != Status.Resolved)
        {
          serviceResponse.Data = await CreateGetTicketDTO(ticket);
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket has not been resolved yet.";
          return serviceResponse;
        }

        ticket.Status = Status.InProgress;
        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync();

        serviceResponse.Data = await CreateGetTicketDTO(ticket);
      }
      catch (Exception ex)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "Unable to re-open ticket with given id.";
        System.Console.WriteLine(ex.Message);
      }

      return serviceResponse;
    }

    public async Task<ServiceResponse<GetTicketDTO>> CancelTicket(TicketIdDTO ticketToCancel)
    {
      ServiceResponse<GetTicketDTO> serviceResponse = new ServiceResponse<GetTicketDTO>();
      var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == _authService.GetUserEmail());

      System.Console.WriteLine("Resolving ticket with id: " + ticketToCancel.TicketId);

      if (user == null)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "User not found.";
        return serviceResponse;
      }

      try
      {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketToCancel.TicketId);
        if (ticket == null)
        {
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket not found.";
          return serviceResponse;
        }
        if (ticket.CompanyId != user.CompanyId)
        {
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket does not belong to your company.";
          return serviceResponse;
        }
        if (ticket.Status == Status.Cancelled)
        {
          serviceResponse.Data = await CreateGetTicketDTO(ticket);
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket has already been cancelled";
          return serviceResponse;
        }
        if (ticket.Status == Status.Resolved)
        {
          serviceResponse.Data = await CreateGetTicketDTO(ticket);
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket has already been resolved.";
          return serviceResponse;
        }

        ticket.Status = Status.Cancelled;
        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync();

        serviceResponse.Data = await CreateGetTicketDTO(ticket);
      }
      catch (Exception ex)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "Unable to cancel ticket with given id.";
        System.Console.WriteLine(ex.Message);
      }

      return serviceResponse;
    }

    public async Task<ServiceResponse<int>> GetTotalTicketsByUser()

    {
      ServiceResponse<int> serviceResponse = new ServiceResponse<int>();
      var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == _authService.GetUserEmail());

      if (user == null)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "User not found.";
        return serviceResponse;
      }

      try
      {
        if (user.Role == Role.VisconAdmin || user.Role == Role.VisconEmployee)
        {
          serviceResponse.Data = await _context.Tickets.Where(t => t.AssigneeId == user.Id).CountAsync();
        }
        else
        {
          serviceResponse.Data = await _context.Tickets.Where(t => t.CreatorId == user.Id).CountAsync();
        }
      }

      catch (Exception ex)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "Unable to get total tickets by user.";
        System.Console.WriteLine(ex.Message);
      }

      return serviceResponse;
    }

    public async Task<ServiceResponse<int>> GetTotalTicketsThisWeek()
    {
      ServiceResponse<int> serviceResponse = new ServiceResponse<int>();
      var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == _authService.GetUserEmail());

      if (user == null)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "User not found.";
        return serviceResponse;
      }

      try
      {
        var compareDate = DateTime.UtcNow.AddDays(-7);

        if (user.Role == Role.VisconAdmin || user.Role == Role.VisconEmployee)
        {
          serviceResponse.Data = await _context.Tickets.Where(t => t.CreationDate > compareDate).CountAsync();
        }
        else
        {
          serviceResponse.Data = await _context.Tickets.Where(t => t.CreatorId == user.Id && t.CreationDate > compareDate).CountAsync();
        }
      }

      catch (Exception ex)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "Unable to get total tickets this week.";
        System.Console.WriteLine(ex.Message);
      }

      return serviceResponse;
    }

    public async Task<ServiceResponse<GetTicketDTO>> AddSolution(AddSolutionDTO solution)
    {
      ServiceResponse<GetTicketDTO> serviceResponse = new ServiceResponse<GetTicketDTO>();
      var requestingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == _authService.GetUserEmail());

      if (requestingUser == null)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "Requesting user not found.";
        return serviceResponse;
      }

      try
      {
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == solution.TicketId);
        if (ticket == null)
        {
          serviceResponse.Success = false;
          serviceResponse.Message = "Ticket not found.";
          return serviceResponse;
        }

        ticket.Solution = solution.Solution;
        _context.Tickets.Update(ticket);
        await _context.SaveChangesAsync();

        serviceResponse.Data = await CreateGetTicketDTO(ticket);
      }
      catch (Exception ex)
      {
        serviceResponse.Success = false;
        serviceResponse.Message = "Unable to add solution to ticket with given id.";
        System.Console.WriteLine(ex.Message);
      }

      return serviceResponse;
    }
  }
}